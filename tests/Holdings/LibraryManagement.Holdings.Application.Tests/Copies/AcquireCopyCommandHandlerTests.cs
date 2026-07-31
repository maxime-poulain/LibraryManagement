using LibraryManagement.Holdings.Application.Copies.AcquireCopy;
using LibraryManagement.Holdings.Application.Tests.TestDoubles;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

public sealed class AcquireCopyCommandHandlerTests
{
    private static readonly Guid CatalogedEdition = Guid.CreateVersion7();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryCopyRepository _copies = new();

    private ValueTask<Result> Handle(AcquireCopyCommand command)
        => new AcquireCopyCommandHandler(_copies, new StubEditionCatalog(CatalogedEdition))
            .Handle(command, Token);

    private static AcquireCopyCommand AnAcquisition(
        Guid? editionId = null,
        string barcode = "30124000512",
        string shelfmark = "843.912 SAI")
        => new(
            Guid.CreateVersion7(),
            editionId ?? CatalogedEdition,
            barcode,
            shelfmark,
            new DateOnly(2024, 3, 14));

    [Fact]
    public async Task Handle_TakesTheCopyIntoTheCollection()
    {
        var outcome = await Handle(AnAcquisition());

        outcome.HasErrors().ShouldBeFalse();

        var copy = _copies.Added.Single();
        copy.Barcode.Value.ShouldBe("30124000512");
        copy.Shelfmark.Value.ShouldBe("843.912 SAI");
        copy.Status.ShouldBe(CopyStatus.InService);
    }

    [Fact]
    public async Task Handle_KeepsTheIdentifierTheCallerChose()
    {
        var command = AnAcquisition();

        await Handle(command);

        _copies.Added.Single().Id.Value.ShouldBe(command.CopyId);
    }

    [Fact]
    public async Task Handle_AnEditionNobodyCataloged_Fails()
    {
        // The one precondition that leaves this context. No foreign key backs the reference, so
        // the question is asked of the module that owns the answer.
        var outcome = await Handle(AnAcquisition(editionId: Guid.CreateVersion7()));

        outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode))
            .ShouldContain(HoldingsErrorCodes.EditionNotFound);
        _copies.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ABarcodeAlreadyOnAnotherCopy_Fails()
    {
        var existing = Copy.Acquire(
            CopyId.Generate(),
            EditionId.Create(CatalogedEdition),
            Barcode.Create("30124000512").Match(b => b, _ => throw new InvalidOperationException()),
            Shelfmark.Create("843.912 SAI").Match(s => s, _ => throw new InvalidOperationException()),
            CopyCondition.Good,
            new DateOnly(2020, 1, 1));
        _copies.With(existing);

        var outcome = await Handle(AnAcquisition(barcode: "30124000512"));

        outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode))
            .ShouldContain(HoldingsErrorCodes.BarcodeAlreadyInUse);
    }

    [Fact]
    public async Task Handle_ReportsEveryRefusalAtOnce()
    {
        // A librarian holding a trolley would rather be told about the barcode and the edition
        // together than discover the second after fixing the first.
        _copies.With(Copy.Acquire(
            CopyId.Generate(),
            EditionId.Create(CatalogedEdition),
            Barcode.Create("30124000512").Match(b => b, _ => throw new InvalidOperationException()),
            Shelfmark.Create("843.912 SAI").Match(s => s, _ => throw new InvalidOperationException()),
            CopyCondition.Good,
            new DateOnly(2020, 1, 1)));

        var outcome = await Handle(
            AnAcquisition(editionId: Guid.CreateVersion7(), barcode: "30124000512"));

        var codes = outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());
        codes.ShouldContain(HoldingsErrorCodes.BarcodeAlreadyInUse);
        codes.ShouldContain(HoldingsErrorCodes.EditionNotFound);
    }

    [Fact]
    public async Task Handle_AMalformedBarcode_IsNotAlsoReportedAsACollision()
    {
        // A barcode that is not one cannot collide with anything, and saying so twice would make a
        // caller fix the wrong thing.
        var outcome = await Handle(AnAcquisition(barcode: "1"));

        var codes = outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());
        codes.ShouldContain(HoldingsErrorCodes.InvalidBarcode);
        codes.ShouldNotContain(HoldingsErrorCodes.BarcodeAlreadyInUse);
    }

    [Fact]
    public async Task Handle_WhenTheDecisionSaysReferenceOnly_HoldsItBackFromTheOutset()
    {
        var command = AnAcquisition() with { ReferenceOnly = true };

        await Handle(command);

        _copies.Added.Single().Status.ShouldBe(CopyStatus.ReferenceOnly);
    }

    [Fact]
    public async Task Handle_RecordsTheAcquisitionDateItWasGiven()
    {
        // Supplied and not stamped: a batch is often accessioned weeks after it was delivered.
        var command = AnAcquisition() with { AcquiredOn = new DateOnly(2019, 11, 4) };

        await Handle(command);

        _copies.Added.Single().AcquiredOn.ShouldBe(new DateOnly(2019, 11, 4));
    }
}
