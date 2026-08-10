using LibraryManagement.Holdings.Application.Copies.RepointCopiesOfMergedEdition;
using LibraryManagement.Holdings.Application.Tests.TestDoubles;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

/// <summary>
/// What a merge in Catalog costs this context: every copy of the absorbed record is filed under the
/// survivor, and nothing else moves.
/// </summary>
public sealed class RepointCopiesOfMergedEditionCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryCopyRepository _copies = new();

    private int _labels;

    private ValueTask<Result> Handle(EditionId absorbed, EditionId surviving)
        => new RepointCopiesOfMergedEditionCommandHandler(_copies)
            .Handle(
                new RepointCopiesOfMergedEditionCommand(absorbed.Value, surviving.Value),
                Token);

    private Copy ACopyOf(EditionId editionId)
    {
        var copy = Copy.Acquire(
            CopyId.Generate(),
            editionId,
            Barcode.Create($"3012400{_labels++:D4}")
                .Match(barcode => barcode, _ => throw new InvalidOperationException()),
            Shelfmark.Create("843.912 SAI")
                .Match(mark => mark, _ => throw new InvalidOperationException()),
            CopyCondition.Good,
            new DateOnly(2026, 1, 5));

        _copies.With(copy);
        copy.ClearDomainEvents();

        return copy;
    }

    [Fact]
    public async Task Handle_MovesEveryCopyOfTheAbsorbedRecord()
    {
        var absorbed = EditionId.Generate();
        var surviving = EditionId.Generate();
        var shelf = new[] { ACopyOf(absorbed), ACopyOf(absorbed), ACopyOf(absorbed) };

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        shelf.ShouldAllBe(copy => copy.EditionId == surviving);
        shelf.ShouldAllBe(copy => copy.DomainEvents.OfType<CopyRepointed>().Count() == 1);
    }

    [Fact]
    public async Task Handle_LeavesTheCopiesOfEveryOtherRecordAlone()
    {
        var absorbed = EditionId.Generate();
        var surviving = EditionId.Generate();
        var another = EditionId.Generate();
        var bystander = ACopyOf(another);
        ACopyOf(absorbed);

        await Handle(absorbed, surviving);

        bystander.EditionId.ShouldBe(another);
        bystander.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ARecordTheLibraryHoldsNoCopyOf_Succeeds()
    {
        // The ordinary case: a cataloger merges records, not shelves. A refusal would make the
        // announcing module's drain replay forever a message it handled correctly.
        var outcome = await Handle(EditionId.Generate(), EditionId.Generate());

        outcome.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ARedeliveredAnnouncement_ChangesNothingASecondTime()
    {
        // Idempotent by the question it asks rather than by a mark it keeps: after the first pass
        // the absorbed identifier is on no copy at all.
        var absorbed = EditionId.Generate();
        var surviving = EditionId.Generate();
        var copy = ACopyOf(absorbed);

        await Handle(absorbed, surviving);
        copy.ClearDomainEvents();

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        copy.EditionId.ShouldBe(surviving);
        copy.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ACopyThatLeftTheCollection_MovesToo()
    {
        // The sweep is deliberately blind to the status, and this is the case that proves it: a
        // withdrawn copy still records which edition it was a copy of.
        var absorbed = EditionId.Generate();
        var surviving = EditionId.Generate();
        var weeded = ACopyOf(absorbed);
        weeded.Withdraw();

        await Handle(absorbed, surviving);

        weeded.EditionId.ShouldBe(surviving);
        weeded.Status.ShouldBe(CopyStatus.Withdrawn);
    }

    [Fact]
    public async Task Handle_DemandsACommand()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await new RepointCopiesOfMergedEditionCommandHandler(_copies).Handle(null!, Token));
    }
}
