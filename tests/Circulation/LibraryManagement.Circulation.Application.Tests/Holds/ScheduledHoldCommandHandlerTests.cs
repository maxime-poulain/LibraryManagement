using LibraryManagement.Circulation.Application.Holds.ExpireUncollectedHolds;
using LibraryManagement.Circulation.Application.Holds.RemindOfHoldsExpiringSoon;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The two hold moments of the daily run: the warning that keeps the design from having to
/// penalise the no-show, and the expiry that makes a queue turn with nobody at the desk.
/// </summary>
public sealed class ScheduledHoldCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlySet<BorrowerId> NobodyBlocked = new HashSet<BorrowerId>();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryHoldQueueRepository _queues = new();
    private readonly StubBalances _balances = new();

    private static CirculationPolicy Policy => CirculationPolicy.Current;

    private HoldQueue AQueueAwaitingPickup(DateOnly deadline, params BorrowerId[] alsoQueued)
    {
        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);

        var placedOn = ThisMorning;

        foreach (var borrower in alsoQueued)
        {
            placedOn = placedOn.AddHours(1);
            queue.PlaceHold(HoldId.Generate(), borrower, placedOn);
        }

        queue.TrapOldestQueued(CopyId.Generate(), deadline, NobodyBlocked);
        queue.ClearDomainEvents();

        _queues.With(queue);
        return queue;
    }

    private ValueTask<Result> RemindOfExpiring()
        => new RemindOfHoldsExpiringSoonCommandHandler(_queues, FrozenClock.At(Today))
            .Handle(new RemindOfHoldsExpiringSoonCommand(), Token);

    private ValueTask<Result> ExpireUncollected()
        => new ExpireUncollectedHoldsCommandHandler(
                _queues, _balances, Policy, FrozenClock.At(Today))
            .Handle(new ExpireUncollectedHoldsCommand(), Token);

    // --- The warning ------------------------------------------------------------------------------

    [Fact]
    public async Task RemindOfExpiring_TellsTheBorrowersWhoseCopyIsAboutToGo()
    {
        var imminent = AQueueAwaitingPickup(Today);
        var later = AQueueAwaitingPickup(Today.AddDays(4));

        (await RemindOfExpiring()).HasErrors().ShouldBeFalse();

        imminent.DomainEvents.OfType<HoldExpiringSoon>().ShouldHaveSingleItem();
        later.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemindOfExpiring_RunTwice_TellsThemOnce()
    {
        var queue = AQueueAwaitingPickup(Today);

        await RemindOfExpiring();
        queue.ClearDomainEvents();
        await RemindOfExpiring();

        queue.DomainEvents.ShouldBeEmpty();
    }

    // --- The expiry -------------------------------------------------------------------------------

    [Fact]
    public async Task ExpireUncollected_EndsTheClaimAndMovesTheCopyOn()
    {
        var next = BorrowerId.Generate();
        var queue = AQueueAwaitingPickup(Today.AddDays(-1), next);

        (await ExpireUncollected()).HasErrors().ShouldBeFalse();

        queue.DomainEvents.OfType<HoldExpired>().ShouldHaveSingleItem();

        var readied = queue.DomainEvents.OfType<HoldReadyForPickup>().Single();
        readied.BorrowerId.ShouldBe(next);
        readied.PickupDeadline.ShouldBe(Today.AddDays(Policy.PickupPeriodInDays));
    }

    [Fact]
    public async Task ExpireUncollected_SkipsTheNextBorrowerWhenADebtBlocksThem()
    {
        var owing = BorrowerId.Generate();
        var clear = BorrowerId.Generate();
        _balances.Owing(owing.Value, 4m);

        var queue = AQueueAwaitingPickup(Today.AddDays(-1), owing, clear);

        await ExpireUncollected();

        queue.DomainEvents.OfType<HoldReadyForPickup>().Single().BorrowerId.ShouldBe(clear);
    }

    [Fact]
    public async Task ExpireUncollected_LeavesAClaimStillWithinItsDeadline()
    {
        var queue = AQueueAwaitingPickup(Today);

        await ExpireUncollected();

        queue.DomainEvents.ShouldBeEmpty();
        queue.Holds.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ExpireUncollected_RunTwice_EndsTheClaimOnce()
    {
        var queue = AQueueAwaitingPickup(Today.AddDays(-1));

        await ExpireUncollected();
        queue.ClearDomainEvents();
        await ExpireUncollected();

        queue.DomainEvents.ShouldBeEmpty();
    }
}
