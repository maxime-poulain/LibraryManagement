using LibraryManagement.Holdings.Application.Copies.DeclareCopyLost;
using LibraryManagement.Holdings.Application.Copies.FindCopy;
using LibraryManagement.Holdings.Application.Copies.RecordCopyCondition;
using LibraryManagement.Holdings.Application.Copies.RelabelCopy;
using LibraryManagement.Holdings.Application.Copies.ReleaseCopyForLending;
using LibraryManagement.Holdings.Application.Copies.ReshelveCopy;
using LibraryManagement.Holdings.Application.Copies.RestrictCopyToReference;
using LibraryManagement.Holdings.Application.Copies.ReturnCopyFromRepair;
using LibraryManagement.Holdings.Application.Copies.SendCopyForRepair;
using LibraryManagement.Holdings.Application.Copies.WithdrawCopy;
using LibraryManagement.Holdings.Application.Tests.TestDoubles;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

/// <summary>
/// The handlers that load a copy and ask it to change. Each is three lines, and each has the same
/// two things worth pinning: it finds the copy the caller named, and it says so plainly when there
/// is none.
/// </summary>
public sealed class CopyCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryCopyRepository _copies = new();

    private static Barcode ABarcode(string value = "30124000512") =>
        Barcode.Create(value).Match(barcode => barcode, _ => throw new InvalidOperationException());

    private static Shelfmark AShelfmark(string value = "843.912 SAI") =>
        Shelfmark.Create(value).Match(mark => mark, _ => throw new InvalidOperationException());

    private Copy AHeldCopy(bool referenceOnly = false)
    {
        var copy = Copy.Acquire(
            CopyId.Generate(),
            EditionId.Generate(),
            ABarcode(),
            AShelfmark(),
            CopyCondition.Good,
            new DateOnly(2024, 3, 14),
            referenceOnly);

        _copies.With(copy);
        return copy;
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    // --- Each handler does what its command says ---------------------------------------------------

    [Fact]
    public async Task Reshelve_MovesTheCopy()
    {
        var copy = AHeldCopy();

        var outcome = await new ReshelveCopyCommandHandler(_copies)
            .Handle(new ReshelveCopyCommand(copy.Id.Value, "JEUN 843.912 SAI"), Token);

        outcome.HasErrors().ShouldBeFalse();
        copy.Shelfmark.Value.ShouldBe("JEUN 843.912 SAI");
    }

    [Fact]
    public async Task Relabel_ReplacesTheLabel()
    {
        var copy = AHeldCopy();

        var outcome = await new RelabelCopyCommandHandler(_copies)
            .Handle(new RelabelCopyCommand(copy.Id.Value, "30124000999"), Token);

        outcome.HasErrors().ShouldBeFalse();
        copy.Barcode.Value.ShouldBe("30124000999");
    }

    [Fact]
    public async Task Relabel_ToALabelAlreadyOnAnotherCopy_Fails()
    {
        var copy = AHeldCopy();
        _copies.With(Copy.Acquire(
            CopyId.Generate(), EditionId.Generate(), ABarcode("30124000999"), AShelfmark(),
            CopyCondition.Good, new DateOnly(2024, 3, 14)));

        var outcome = await new RelabelCopyCommandHandler(_copies)
            .Handle(new RelabelCopyCommand(copy.Id.Value, "30124000999"), Token);

        CodesOf(outcome).ShouldContain(HoldingsErrorCodes.BarcodeAlreadyInUse);
        copy.Barcode.Value.ShouldBe("30124000512");
    }

    [Fact]
    public async Task Relabel_ToTheLabelItAlreadyCarries_IsNotACollisionWithItself()
    {
        var copy = AHeldCopy();

        var outcome = await new RelabelCopyCommandHandler(_copies)
            .Handle(new RelabelCopyCommand(copy.Id.Value, "30124000512"), Token);

        outcome.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task RecordCondition_RecordsIt()
    {
        var copy = AHeldCopy();

        var outcome = await new RecordCopyConditionCommandHandler(_copies)
            .Handle(new RecordCopyConditionCommand(copy.Id.Value, CopyCondition.Damaged), Token);

        outcome.HasErrors().ShouldBeFalse();
        copy.Condition.ShouldBe(CopyCondition.Damaged);
    }

    [Fact]
    public async Task SendForRepair_AndBack_ReturnsAReferenceOnlyCopyToReferenceOnly()
    {
        var copy = AHeldCopy(referenceOnly: true);

        await new SendCopyForRepairCommandHandler(_copies)
            .Handle(new SendCopyForRepairCommand(copy.Id.Value), Token);
        copy.Status.ShouldBe(CopyStatus.InRepair);

        await new ReturnCopyFromRepairCommandHandler(_copies)
            .Handle(new ReturnCopyFromRepairCommand(copy.Id.Value), Token);

        copy.Status.ShouldBe(CopyStatus.ReferenceOnly);
    }

    [Fact]
    public async Task RestrictToReference_AndReleaseForLending_MoveTheCopyBetweenCollections()
    {
        var copy = AHeldCopy();

        await new RestrictCopyToReferenceCommandHandler(_copies)
            .Handle(new RestrictCopyToReferenceCommand(copy.Id.Value), Token);
        copy.Status.ShouldBe(CopyStatus.ReferenceOnly);

        await new ReleaseCopyForLendingCommandHandler(_copies)
            .Handle(new ReleaseCopyForLendingCommand(copy.Id.Value), Token);

        copy.Status.ShouldBe(CopyStatus.InService);
    }

    [Fact]
    public async Task DeclareLost_AndFind_TakeTheCopyOutAndPutItBack()
    {
        var copy = AHeldCopy();

        await new DeclareCopyLostCommandHandler(_copies)
            .Handle(new DeclareCopyLostCommand(copy.Id.Value), Token);
        copy.Status.ShouldBe(CopyStatus.Lost);

        await new FindCopyCommandHandler(_copies)
            .Handle(new FindCopyCommand(copy.Id.Value), Token);

        copy.Status.ShouldBe(CopyStatus.InService);
    }

    [Fact]
    public async Task Withdraw_RemovesTheCopyFromTheCollection()
    {
        var copy = AHeldCopy();

        var outcome = await new WithdrawCopyCommandHandler(_copies)
            .Handle(new WithdrawCopyCommand(copy.Id.Value), Token);

        outcome.HasErrors().ShouldBeFalse();
        copy.Status.ShouldBe(CopyStatus.Withdrawn);
    }

    // --- And each says so when there is no such copy ------------------------------------------------

    [Fact]
    public async Task ACopyNobodyHolds_IsReportedAsSuchByEveryHandler()
    {
        var absent = Guid.CreateVersion7();

        IReadOnlyList<Result> outcomes =
        [
            await new ReshelveCopyCommandHandler(_copies)
                .Handle(new ReshelveCopyCommand(absent, "843.912 SAI"), Token),
            await new RelabelCopyCommandHandler(_copies)
                .Handle(new RelabelCopyCommand(absent, "30124000999"), Token),
            await new RecordCopyConditionCommandHandler(_copies)
                .Handle(new RecordCopyConditionCommand(absent, CopyCondition.Worn), Token),
            await new SendCopyForRepairCommandHandler(_copies)
                .Handle(new SendCopyForRepairCommand(absent), Token),
            await new ReturnCopyFromRepairCommandHandler(_copies)
                .Handle(new ReturnCopyFromRepairCommand(absent), Token),
            await new RestrictCopyToReferenceCommandHandler(_copies)
                .Handle(new RestrictCopyToReferenceCommand(absent), Token),
            await new ReleaseCopyForLendingCommandHandler(_copies)
                .Handle(new ReleaseCopyForLendingCommand(absent), Token),
            await new DeclareCopyLostCommandHandler(_copies)
                .Handle(new DeclareCopyLostCommand(absent), Token),
            await new FindCopyCommandHandler(_copies)
                .Handle(new FindCopyCommand(absent), Token),
            await new WithdrawCopyCommandHandler(_copies)
                .Handle(new WithdrawCopyCommand(absent), Token),
        ];

        outcomes.ShouldAllBe(outcome => CodesOf(outcome).Contains(HoldingsErrorCodes.CopyNotFound));
    }
}
