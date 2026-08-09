using LibraryManagement.Circulation.Application.Holds.CancelUnfulfillableHolds;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The daily sweep for queues that survived their editions: a claim is a promise of the next
/// available copy, and an edition with nothing left to serve has no next to promise.
/// </summary>
public sealed class CancelUnfulfillableHoldsCommandHandlerTests
{
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubShelf _copies = new();

    private ValueTask<Result> Handle()
        => new CancelUnfulfillableHoldsCommandHandler(_queues, _copies)
            .Handle(new CancelUnfulfillableHoldsCommand(), Token);

    private HoldQueue AQueueWithOneClaim(Guid edition)
    {
        var queue = HoldQueue.For(EditionId.Create(edition));
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        queue.ClearDomainEvents();
        _queues.With(queue);
        return queue;
    }

    [Fact]
    public async Task Handle_EndsTheClaims_OfTheEditionNothingCanServe()
    {
        // The manga whose only copy was declared lost and cannot be bought back this year: the
        // eleven people in its queue stop waiting for nothing, each told out loud.
        var deadEdition = Guid.CreateVersion7();
        var livingEdition = Guid.CreateVersion7();
        var dead = AQueueWithOneClaim(deadEdition);
        var living = AQueueWithOneClaim(livingEdition);
        _copies.NothingLeftToServe(deadEdition);

        var outcome = await Handle();

        outcome.HasErrors().ShouldBeFalse();
        dead.Holds.ShouldBeEmpty();
        dead.DomainEvents.OfType<HoldCancelledUnfulfillable>().ShouldHaveSingleItem();
        living.Holds.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_ACopyInRepair_KeepsTheQueueAlive()
    {
        // Repair counts as yes: a copy in repair is expected back in the stock, and a queue that
        // waits on it waits on something real. The stub answers 'something to serve' unless told
        // otherwise, which is exactly the port's answer for an edition with a copy in repair.
        var queue = AQueueWithOneClaim(Guid.CreateVersion7());

        (await Handle()).HasErrors().ShouldBeFalse();

        queue.Holds.ShouldHaveSingleItem();
    }
}
