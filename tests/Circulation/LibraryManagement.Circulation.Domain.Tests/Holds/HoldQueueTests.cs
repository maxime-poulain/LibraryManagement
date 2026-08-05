using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using static LibraryManagement.Circulation.Domain.Tests.Desk;

namespace LibraryManagement.Circulation.Domain.Tests.Holds;

public sealed class HoldQueueTests
{
    private static readonly IReadOnlySet<BorrowerId> NobodyBlocked = new HashSet<BorrowerId>();

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    private static DateOnly Deadline => Today.AddDays(Policy.PickupPeriodInDays);

    // --- Placing -----------------------------------------------------------------------------------

    [Fact]
    public void PlaceHold_JoinsTheQueue_AndSaysSo()
    {
        var queue = AQueue();
        var holdId = HoldId.Generate();
        var borrowerId = BorrowerId.Generate();

        queue.PlaceHold(holdId, borrowerId, ThisMorning).HasErrors().ShouldBeFalse();

        queue.Holds.Count.ShouldBe(1);
        queue.AnyoneIsWaiting.ShouldBeTrue();

        var placed = queue.Event<HoldPlaced>();
        placed.EditionId.ShouldBe(queue.Id);
        placed.HoldId.ShouldBe(holdId);
        placed.BorrowerId.ShouldBe(borrowerId);
    }

    [Fact]
    public void PlaceHold_Twice_IsRefused()
    {
        // A borrower appears at most once in a queue.
        var queue = AQueue();
        var borrowerId = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), borrowerId, ThisMorning);

        var outcome = queue.PlaceHold(HoldId.Generate(), borrowerId, ThisMorning.AddHours(1));

        CodesOf(outcome).ShouldContain(CirculationErrorCodes.HoldAlreadyPlaced);
        queue.Holds.Count.ShouldBe(1);
    }

    [Fact]
    public void QueuedBorrowers_ComeOldestFirst_WhateverTheStorageOrder()
    {
        var queue = AQueue();
        var second = BorrowerId.Generate();
        var first = BorrowerId.Generate();

        queue.PlaceHold(HoldId.Generate(), second, ThisMorning.AddHours(2));
        queue.PlaceHold(HoldId.Generate(), first, ThisMorning);

        queue.QueuedBorrowersInOrder().ShouldBe([first, second]);
    }

    // --- Trapping ----------------------------------------------------------------------------------

    [Fact]
    public void TrapOldestQueued_ServesThePlacementOrder()
    {
        var queue = AQueue();
        var first = BorrowerId.Generate();
        var second = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), first, ThisMorning);
        queue.PlaceHold(HoldId.Generate(), second, ThisMorning.AddHours(1));
        queue.ClearDomainEvents();

        var copyId = CopyId.Generate();
        var trapped = queue.TrapOldestQueued(copyId, Deadline, NobodyBlocked);

        trapped.ShouldNotBeNull().BorrowerId.ShouldBe(first);
        trapped.Status.ShouldBe(HoldStatus.AwaitingPickup);
        trapped.TrappedCopyId.ShouldBe(copyId);
        trapped.PickupDeadline.ShouldBe(Deadline);

        var ready = queue.Event<HoldReadyForPickup>();
        ready.BorrowerId.ShouldBe(first);
        ready.CopyId.ShouldBe(copyId);
        ready.PickupDeadline.ShouldBe(Deadline);
    }

    [Fact]
    public void TrapOldestQueued_SkipsABlockedBorrower_WithoutRemovingThem()
    {
        // Skipped, never removed: the debt event may simply not have arrived yet, and the removal
        // is that handler's job when it does.
        var queue = AQueue();
        var blocked = BorrowerId.Generate();
        var clear = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), blocked, ThisMorning);
        queue.PlaceHold(HoldId.Generate(), clear, ThisMorning.AddHours(1));

        var trapped = queue.TrapOldestQueued(
            CopyId.Generate(), Deadline, new HashSet<BorrowerId> { blocked });

        trapped.ShouldNotBeNull().BorrowerId.ShouldBe(clear);
        queue.Holds.Count.ShouldBe(2);
    }

    [Fact]
    public void TrapOldestQueued_WhenNobodyQualifies_LeavesTheCopyToTheShelf()
    {
        var queue = AQueue();
        var blocked = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), blocked, ThisMorning);
        queue.ClearDomainEvents();

        var trapped = queue.TrapOldestQueued(
            CopyId.Generate(), Deadline, new HashSet<BorrowerId> { blocked });

        trapped.ShouldBeNull();
        queue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void SeveralHoldsAwaitingPickupAtOnce_AreOrdinary()
    {
        // Three copies returning on one morning trap for the first three in the queue.
        var queue = AQueue();
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning.AddHours(1));

        queue.TrapOldestQueued(CopyId.Generate(), Deadline, NobodyBlocked).ShouldNotBeNull();
        queue.TrapOldestQueued(CopyId.Generate(), Deadline, NobodyBlocked).ShouldNotBeNull();

        queue.Holds.Count(hold => hold.Status == HoldStatus.AwaitingPickup).ShouldBe(2);
        queue.AnyoneIsWaiting.ShouldBeFalse();
    }

    [Fact]
    public void TheSameCopy_CannotBePromisedTwice()
    {
        // Offering an already-trapped copy is a defect in the caller, not a refusal to report
        // politely.
        var queue = AQueue();
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning.AddHours(1));

        var copyId = CopyId.Generate();
        queue.TrapOldestQueued(copyId, Deadline, NobodyBlocked);

        Should.Throw<InvalidOperationException>(
            () => queue.TrapOldestQueued(copyId, Deadline, NobodyBlocked));
    }

    [Fact]
    public void AwaitingPickup_DoesNotHoldUpARenewal()
    {
        // The claim already has its copy on the hold shelf; refusing a renewal for its sake would
        // serve nobody, and the rule exists so the queue turns for the people still waiting.
        var queue = AQueue();
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        queue.TrapOldestQueued(CopyId.Generate(), Deadline, NobodyBlocked);

        queue.AnyoneIsWaiting.ShouldBeFalse();
    }

    // --- Fulfilling --------------------------------------------------------------------------------

    [Fact]
    public void Fulfill_EndsTheClaim_AndTheOutcomeTravelsInTheEvent()
    {
        var queue = AQueue();
        var borrowerId = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), borrowerId, ThisMorning);
        var copyId = CopyId.Generate();
        queue.TrapOldestQueued(copyId, Deadline, NobodyBlocked);
        queue.ClearDomainEvents();

        queue.Fulfill(copyId).HasErrors().ShouldBeFalse();

        queue.Holds.ShouldBeEmpty();
        var fulfilled = queue.Event<HoldFulfilled>();
        fulfilled.BorrowerId.ShouldBe(borrowerId);
        fulfilled.CopyId.ShouldBe(copyId);
    }

    [Fact]
    public void Fulfill_ACopyNobodyWaitsFor_IsRefused()
    {
        var queue = AQueue();

        CodesOf(queue.Fulfill(CopyId.Generate())).ShouldContain(CirculationErrorCodes.NoSuchHold);
    }

    // --- Cancelling --------------------------------------------------------------------------------

    [Fact]
    public void CancelFor_AQueuedClaim_ClosesTheGapBehindIt()
    {
        var queue = AQueue();
        var first = BorrowerId.Generate();
        var second = BorrowerId.Generate();
        var third = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), first, ThisMorning);
        queue.PlaceHold(HoldId.Generate(), second, ThisMorning.AddHours(1));
        queue.PlaceHold(HoldId.Generate(), third, ThisMorning.AddHours(2));
        queue.ClearDomainEvents();

        var outcome = queue.CancelFor(second);

        outcome.HasErrors().ShouldBeFalse();
        outcome.Match<CopyId?>(cancellation => cancellation.ReleasedCopyId, _ => null).ShouldBeNull();
        queue.QueuedBorrowersInOrder().ShouldBe([first, third]);
        queue.Event<HoldCancelled>().BorrowerId.ShouldBe(second);
    }

    [Fact]
    public void CancelFor_AClaimAwaitingPickup_HandsTheCopyBack()
    {
        var queue = AQueue();
        var borrowerId = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), borrowerId, ThisMorning);
        var copyId = CopyId.Generate();
        queue.TrapOldestQueued(copyId, Deadline, NobodyBlocked);

        var outcome = queue.CancelFor(borrowerId);

        outcome.Match<CopyId?>(cancellation => cancellation.ReleasedCopyId, _ => null).ShouldBe(copyId);
        queue.Holds.ShouldBeEmpty();
    }

    [Fact]
    public void CancelFor_ABorrowerWithNoClaim_IsRefused()
    {
        var queue = AQueue();

        CodesOf(queue.CancelFor(BorrowerId.Generate()).ToResult())
            .ShouldContain(CirculationErrorCodes.NoSuchHold);
    }
}
