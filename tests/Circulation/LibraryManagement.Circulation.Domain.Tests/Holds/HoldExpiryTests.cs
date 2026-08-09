using LibraryManagement.Circulation.Domain.Holds;
using static LibraryManagement.Circulation.Domain.Tests.Desk;

namespace LibraryManagement.Circulation.Domain.Tests.Holds;

/// <summary>
/// What the scheduled process asks of a queue: warn whoever is about to lose their copy, and end
/// the claims nobody came for — moving each released copy on to the next in line.
/// </summary>
public sealed class HoldExpiryTests
{
    private static readonly IReadOnlySet<BorrowerId> NobodyBlocked = new HashSet<BorrowerId>();

    private static HoldQueue AQueueWithATrappedCopy(
        out BorrowerId collector,
        out CopyId copyId,
        DateOnly deadline)
    {
        var queue = AQueue();
        collector = BorrowerId.Generate();
        copyId = CopyId.Generate();

        queue.PlaceHold(HoldId.Generate(), collector, ThisMorning);
        queue.TrapOldestQueued(copyId, deadline, NobodyBlocked);
        queue.ClearDomainEvents();

        return queue;
    }

    // --- Warning --------------------------------------------------------------------------------

    [Fact]
    public void WarnOfImminentExpiry_OnTheDeadlineDay_SaysSo()
    {
        var queue = AQueueWithATrappedCopy(out var collector, out var copyId, Today);

        queue.WarnOfImminentExpiry(Today);

        var warning = queue.Event<HoldExpiringSoon>();
        warning.EditionId.ShouldBe(queue.Id);
        warning.BorrowerId.ShouldBe(collector);
        warning.CopyId.ShouldBe(copyId);
        warning.PickupDeadline.ShouldBe(Today);
        queue.Holds.Single().ExpiryWarningSent.ShouldBeTrue();
    }

    [Fact]
    public void WarnOfImminentExpiry_TheDayBefore_SaysSoToo()
    {
        // Two days wide, so a run that misses a day still warns somebody before their copy goes.
        var queue = AQueueWithATrappedCopy(out _, out _, Today.AddDays(1));

        queue.WarnOfImminentExpiry(Today);

        queue.DomainEvents.OfType<HoldExpiringSoon>().ShouldHaveSingleItem();
    }

    [Fact]
    public void WarnOfImminentExpiry_Twice_SaysItOnce()
    {
        var queue = AQueueWithATrappedCopy(out _, out _, Today);
        queue.WarnOfImminentExpiry(Today);
        queue.ClearDomainEvents();

        queue.WarnOfImminentExpiry(Today);

        queue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void WarnOfImminentExpiry_WhileTheDeadlineIsStillFarOff_SaysNothing()
    {
        var queue = AQueueWithATrappedCopy(out _, out _, Today.AddDays(3));

        queue.WarnOfImminentExpiry(Today);

        queue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void WarnOfImminentExpiry_OnAClaimAlreadyPastItsDeadline_SaysNothing()
    {
        // It is expired in the same run; "hurry up" about a copy that has just gone back is worse
        // than silence.
        var queue = AQueueWithATrappedCopy(out _, out _, Today.AddDays(-1));

        queue.WarnOfImminentExpiry(Today);

        queue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void WarnOfImminentExpiry_OnAQueuedClaim_SaysNothing()
    {
        var queue = AQueue();
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        queue.ClearDomainEvents();

        queue.WarnOfImminentExpiry(Today);

        queue.DomainEvents.ShouldBeEmpty();
    }

    // --- Expiry ---------------------------------------------------------------------------------

    [Fact]
    public void ExpireUncollectedHolds_PastTheDeadline_EndsTheClaim()
    {
        var queue = AQueueWithATrappedCopy(out var collector, out var copyId, Today.AddDays(-1));

        queue.ExpireUncollectedHolds(Today, NobodyBlocked, Policy);

        var expired = queue.Event<HoldExpired>();
        expired.BorrowerId.ShouldBe(collector);
        expired.CopyId.ShouldBe(copyId);
        queue.Holds.ShouldBeEmpty();
    }

    [Fact]
    public void ExpireUncollectedHolds_OnTheDeadlineDayItself_LeavesItAlone()
    {
        // The deadline is the last day the copy waits, so a claim lapses the day after it.
        var queue = AQueueWithATrappedCopy(out _, out _, Today);

        queue.ExpireUncollectedHolds(Today, NobodyBlocked, Policy);

        queue.DomainEvents.ShouldBeEmpty();
        queue.Holds.ShouldHaveSingleItem();
    }

    [Fact]
    public void ExpireUncollectedHolds_HandsTheCopyToTheNextInLine()
    {
        var queue = AQueue();
        var collector = BorrowerId.Generate();
        var next = BorrowerId.Generate();
        var copyId = CopyId.Generate();

        queue.PlaceHold(HoldId.Generate(), collector, ThisMorning);
        queue.PlaceHold(HoldId.Generate(), next, ThisMorning.AddHours(1));
        queue.TrapOldestQueued(copyId, Today.AddDays(-1), NobodyBlocked);
        queue.ClearDomainEvents();

        queue.ExpireUncollectedHolds(Today, NobodyBlocked, Policy);

        var readied = queue.Event<HoldReadyForPickup>();
        readied.BorrowerId.ShouldBe(next);
        readied.CopyId.ShouldBe(copyId);
        readied.PickupDeadline.ShouldBe(Today.AddDays(Policy.PickupPeriodInDays));

        queue.Holds.Single().BorrowerId.ShouldBe(next);
    }

    [Fact]
    public void ExpireUncollectedHolds_SkipsABlockedBorrower_WhenOfferingTheCopyOn()
    {
        var queue = AQueue();
        var collector = BorrowerId.Generate();
        var owing = BorrowerId.Generate();
        var clear = BorrowerId.Generate();

        queue.PlaceHold(HoldId.Generate(), collector, ThisMorning);
        queue.PlaceHold(HoldId.Generate(), owing, ThisMorning.AddHours(1));
        queue.PlaceHold(HoldId.Generate(), clear, ThisMorning.AddHours(2));
        queue.TrapOldestQueued(CopyId.Generate(), Today.AddDays(-1), NobodyBlocked);
        queue.ClearDomainEvents();

        queue.ExpireUncollectedHolds(Today, new HashSet<BorrowerId> { owing }, Policy);

        queue.Event<HoldReadyForPickup>().BorrowerId.ShouldBe(clear);
        queue.Holds.Count.ShouldBe(2);
    }

    [Fact]
    public void ExpireUncollectedHolds_WithNobodyLeftToOfferItTo_JustEndsTheClaim()
    {
        var queue = AQueueWithATrappedCopy(out _, out _, Today.AddDays(-1));

        queue.ExpireUncollectedHolds(Today, NobodyBlocked, Policy);

        queue.DomainEvents.OfType<HoldExpired>().ShouldHaveSingleItem();
        queue.DomainEvents.OfType<HoldReadyForPickup>().ShouldBeEmpty();
    }

    [Fact]
    public void ExpireUncollectedHolds_Twice_EndsTheClaimOnce()
    {
        // Idempotent by construction rather than by a flag: an expired claim has left the queue.
        var queue = AQueueWithATrappedCopy(out _, out _, Today.AddDays(-1));
        queue.ExpireUncollectedHolds(Today, NobodyBlocked, Policy);
        queue.ClearDomainEvents();

        queue.ExpireUncollectedHolds(Today, NobodyBlocked, Policy);

        queue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void ExpireUncollectedHolds_WhoseDeadlineAClosureTook_WaitsForTheFirstOpenDayToPass()
    {
        // The stored deadline fell on a day the library never opened — a strike, declared after
        // the copy was set aside. Losing the claim for not walking through a locked door is the
        // injustice the calendar exists to remove, so the deadline is judged through the calendar
        // as it stands: the first open day stands in for the closed one, and only its passing
        // expires the claim.
        var queue = AQueueWithATrappedCopy(out _, out _, Today.AddDays(-1));
        var strike = Policy with
        {
            Calendar = new OpeningCalendar([], [Today.AddDays(-1)]),
        };

        queue.ExpireUncollectedHolds(Today, NobodyBlocked, strike);
        queue.DomainEvents.ShouldBeEmpty();

        queue.ExpireUncollectedHolds(Today.AddDays(1), NobodyBlocked, strike);
        queue.DomainEvents.OfType<HoldExpired>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ExpireUncollectedHolds_HandsTheCopyOn_WithADeadlineSlidOffAClosedDay()
    {
        // The next borrower's seven days land on a closed Sunday: the last day their copy waits
        // must be one they can walk in on, so it slides past the closed Monday too. The run
        // itself keeps no desk hours — expiring on a Sunday is ordinary.
        var closedSundayAndMonday = Policy with
        {
            Calendar = new OpeningCalendar([DayOfWeek.Sunday, DayOfWeek.Monday], []),
        };
        var queue = AQueue();
        var next = BorrowerId.Generate();

        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        queue.PlaceHold(HoldId.Generate(), next, ThisMorning.AddHours(1));
        queue.TrapOldestQueued(CopyId.Generate(), Today, NobodyBlocked);
        queue.ClearDomainEvents();

        queue.ExpireUncollectedHolds(Today.AddDays(1), NobodyBlocked, closedSundayAndMonday);

        var readied = queue.Event<HoldReadyForPickup>();
        readied.BorrowerId.ShouldBe(next);
        readied.PickupDeadline.ShouldBe(Today.AddDays(10));
    }

    [Fact]
    public void AClaimHandedASecondCopy_MayBeWarnedAboutItInTurn()
    {
        // A fresh deadline is a fresh appointment, so the warning already spent on the first copy
        // must not silence the second.
        var queue = AQueue();
        var collector = BorrowerId.Generate();
        var next = BorrowerId.Generate();

        queue.PlaceHold(HoldId.Generate(), collector, ThisMorning);
        queue.PlaceHold(HoldId.Generate(), next, ThisMorning.AddHours(1));
        queue.TrapOldestQueued(CopyId.Generate(), Today, NobodyBlocked);
        queue.WarnOfImminentExpiry(Today);

        // The collector never came, so the copy moves on with a deadline of its own.
        queue.ExpireUncollectedHolds(Today.AddDays(1), NobodyBlocked, Policy);
        queue.ClearDomainEvents();

        var newDeadline = Today.AddDays(1 + Policy.PickupPeriodInDays);
        queue.WarnOfImminentExpiry(newDeadline);

        queue.Event<HoldExpiringSoon>().BorrowerId.ShouldBe(next);
    }
}
