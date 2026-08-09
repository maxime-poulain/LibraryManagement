using LibraryManagement.Holdings.Application.Copies.NoteCopyAccountedFor;
using LibraryManagement.Holdings.Application.Tests.TestDoubles;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

/// <summary>
/// The record made to agree with the shelf: a copy that passed over the desk cannot be
/// unaccounted for.
/// </summary>
public sealed class NoteCopyAccountedForCommandHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryCopyRepository _copies = new();

    private ValueTask<Result> Handle(Guid copyId)
        => new NoteCopyAccountedForCommandHandler(_copies)
            .Handle(new NoteCopyAccountedForCommand(copyId), Token);

    private Copy ACopy()
    {
        var copy = Copy.Acquire(
            CopyId.Generate(),
            EditionId.Generate(),
            Barcode.Create("30124").Match(barcode => barcode, _ => throw new InvalidOperationException()),
            Shelfmark.Create("843.912 SAI").Match(mark => mark, _ => throw new InvalidOperationException()),
            CopyCondition.Good,
            new DateOnly(2026, 1, 5));
        _copies.With(copy);
        return copy;
    }

    [Fact]
    public async Task Handle_FindsACopyHeldAsLost()
    {
        // Declared lost at a stocktake's word while out on loan, then returned as if nothing had
        // happened: the record said 'unaccounted for' about an object in hand.
        var copy = ACopy();
        copy.DeclareLost();
        copy.ClearDomainEvents();

        var outcome = await Handle(copy.Id.Value);

        outcome.HasErrors().ShouldBeFalse();
        copy.Status.ShouldBe(CopyStatus.InService);
        copy.DomainEvents.OfType<CopyFound>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ACopyInGoodStanding_NotesNothing()
    {
        // Almost every return lands here: the record already matches the shelf.
        var copy = ACopy();
        copy.ClearDomainEvents();

        var outcome = await Handle(copy.Id.Value);

        outcome.HasErrors().ShouldBeFalse();
        copy.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ACopyThisModuleNeverHeardOf_AnswersSuccess()
    {
        (await Handle(Guid.CreateVersion7())).HasErrors().ShouldBeFalse();
    }
}
