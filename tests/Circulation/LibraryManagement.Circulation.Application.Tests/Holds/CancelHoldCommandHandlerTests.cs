using LibraryManagement.Circulation.Application.Holds.CancelHold;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

public sealed class CancelHoldCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubBalances _balances = new();

    private ValueTask<Result> Handle(Guid edition, Guid borrower)
        => new CancelHoldCommandHandler(
                _queues, _balances, CirculationPolicy.Current, FrozenClock.At(Today))
            .Handle(new CancelHoldCommand(edition, borrower), Token);

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_RemovesAQueuedClaim()
    {
        var borrower = BorrowerId.Generate();
        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), borrower, DateTimeOffset.UnixEpoch);
        _queues.With(queue);

        var outcome = await Handle(queue.Id.Value, borrower.Value);

        outcome.HasErrors().ShouldBeFalse();
        queue.Holds.ShouldBeEmpty();
        queue.DomainEvents.OfType<HoldCancelled>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_AReleasedCopy_IsOfferedToTheNextInGoodStanding()
    {
        // Exactly as an expiry will release it, when the scheduled process exists: a copy on the
        // hold shelf that no live claim owns is a promise to nobody.
        var collector = BorrowerId.Generate();
        var next = BorrowerId.Generate();
        var copy = CopyId.Generate();

        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), collector, DateTimeOffset.UnixEpoch);
        queue.PlaceHold(HoldId.Generate(), next, DateTimeOffset.UnixEpoch.AddHours(1));
        queue.TrapOldestQueued(copy, Today.AddDays(7), new HashSet<BorrowerId>());
        _queues.With(queue);

        var outcome = await Handle(queue.Id.Value, collector.Value);

        outcome.HasErrors().ShouldBeFalse();

        var ready = queue.DomainEvents.OfType<HoldReadyForPickup>()
            .Single(readied => readied.BorrowerId == next);
        ready.CopyId.ShouldBe(copy);
        ready.PickupDeadline.ShouldBe(
            Today.AddDays(CirculationPolicy.Current.PickupPeriodInDays));
    }

    [Fact]
    public async Task Handle_AReleasedCopy_StaysOnTheShelfWhenTheNextIsBlocked()
    {
        var collector = BorrowerId.Generate();
        var owing = BorrowerId.Generate();
        _balances.Owing(owing.Value, 1m);

        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), collector, DateTimeOffset.UnixEpoch);
        queue.PlaceHold(HoldId.Generate(), owing, DateTimeOffset.UnixEpoch.AddHours(1));
        queue.TrapOldestQueued(CopyId.Generate(), Today.AddDays(7), new HashSet<BorrowerId>());
        queue.ClearDomainEvents();
        _queues.With(queue);

        var outcome = await Handle(queue.Id.Value, collector.Value);

        outcome.HasErrors().ShouldBeFalse();
        queue.DomainEvents.OfType<HoldReadyForPickup>().ShouldBeEmpty();
        queue.Holds.Single().Status.ShouldBe(HoldStatus.Queued);
    }

    [Fact]
    public async Task Handle_AnEditionWithNoQueue_IsRefused()
    {
        CodesOf(await Handle(Guid.CreateVersion7(), Guid.CreateVersion7()))
            .ShouldContain(CirculationErrorCodes.NoSuchHold);
    }

    [Fact]
    public async Task Handle_ABorrowerWithNoClaimThere_IsRefused()
    {
        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), DateTimeOffset.UnixEpoch);
        _queues.With(queue);

        CodesOf(await Handle(queue.Id.Value, Guid.CreateVersion7()))
            .ShouldContain(CirculationErrorCodes.NoSuchHold);
    }
}
