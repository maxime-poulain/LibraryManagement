using LibraryManagement.Circulation.Domain.Holds;
using static LibraryManagement.Circulation.Domain.Tests.Desk;

namespace LibraryManagement.Circulation.Domain.Tests.Holds;

/// <summary>
/// What a merge in Members costs one queue: two borrowers' claims become one borrower's, under the
/// survival rules the queue merge already wrote — a claim awaiting pickup always survives, and
/// among the queued ones the earliest does.
/// </summary>
public sealed class HoldQueueCombineClaimsTests
{
    private static Hold ClaimOf(HoldQueue queue, BorrowerId borrowerId, int hoursIn = 0)
    {
        var holdId = HoldId.Generate();
        queue.PlaceHold(holdId, borrowerId, ThisMorning.AddHours(hoursIn));
        return queue.Holds.Single(hold => hold.Id == holdId);
    }

    [Fact]
    public void CombineClaimsOf_MovesEveryClaimOfTheAbsorbedRecord()
    {
        var queue = AQueue();
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var bystander = BorrowerId.Generate();
        var claim = ClaimOf(queue, absorbed);
        var theirs = ClaimOf(queue, bystander, hoursIn: 1);
        queue.Settled();

        queue.CombineClaimsOf(absorbed, surviving);

        claim.BorrowerId.ShouldBe(surviving);
        theirs.BorrowerId.ShouldBe(bystander);

        var repointed = queue.Event<HoldBorrowerRepointed>();
        repointed.EditionId.ShouldBe(queue.Id);
        repointed.HoldId.ShouldBe(claim.Id);
        repointed.PreviousBorrowerId.ShouldBe(absorbed);
        repointed.NewBorrowerId.ShouldBe(surviving);
    }

    [Fact]
    public void CombineClaimsOf_TwoQueuedClaims_KeepTheEarliest_WhicheverFileItCameFrom()
    {
        // The surviving *file* does not win; the earliest *claim* does. The person asked for the
        // book the first time under the record that happened to be absorbed, and their wait is as
        // old as that first asking.
        var queue = AQueue();
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var earlier = ClaimOf(queue, absorbed, hoursIn: 0);
        var later = ClaimOf(queue, surviving, hoursIn: 1);
        queue.Settled();

        queue.CombineClaimsOf(absorbed, surviving);

        var remaining = queue.Holds.ShouldHaveSingleItem();
        remaining.Id.ShouldBe(earlier.Id);
        remaining.BorrowerId.ShouldBe(surviving);

        var cancelled = queue.Event<HoldCancelledAsDuplicate>();
        cancelled.HoldId.ShouldBe(later.Id);
        cancelled.SurvivingHoldId.ShouldBe(earlier.Id);
    }

    [Fact]
    public void CombineClaimsOf_AClaimAwaitingPickup_AlwaysSurvives()
    {
        // A copy is physically on the hold shelf with the person's name on it. The merged file
        // legitimately holds one queued claim and one copy set aside — the state the restated
        // invariant permits and the desk still refuses to create.
        var queue = AQueue();
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        ClaimOf(queue, absorbed, hoursIn: 0);
        ClaimOf(queue, surviving, hoursIn: 1);
        queue.TrapOldestQueued(CopyId.Generate(), Today.AddDays(7), new HashSet<BorrowerId>());
        queue.Settled();

        queue.CombineClaimsOf(absorbed, surviving);

        queue.Holds.Count.ShouldBe(2);
        queue.Holds.ShouldAllBe(hold => hold.BorrowerId == surviving);
        queue.Holds.Count(hold => hold.Status == HoldStatus.AwaitingPickup).ShouldBe(1);
        queue.DomainEvents.OfType<HoldCancelledAsDuplicate>().ShouldBeEmpty();
    }

    [Fact]
    public void CombineClaimsOf_TwoCopiesSetAside_BothStaySetAside()
    {
        // The rare double trap: each file was promised a copy before anyone knew they were one
        // person, and both promises were made in the world. The filtered unique index on the
        // trapped copy spans queues, so the store agrees.
        var queue = AQueue();
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        ClaimOf(queue, absorbed, hoursIn: 0);
        ClaimOf(queue, surviving, hoursIn: 1);
        queue.TrapOldestQueued(CopyId.Generate(), Today.AddDays(7), new HashSet<BorrowerId>());
        queue.TrapOldestQueued(CopyId.Generate(), Today.AddDays(7), new HashSet<BorrowerId>());
        queue.Settled();

        queue.CombineClaimsOf(absorbed, surviving);

        queue.Holds.Count.ShouldBe(2);
        queue.Holds.ShouldAllBe(hold => hold.Status == HoldStatus.AwaitingPickup);
        queue.TrappedCopyIds().Count.ShouldBe(2);
    }

    [Fact]
    public void CombineClaimsOf_AQueueTheAbsorbedRecordDoesNotWaitIn_RecordsNothing()
    {
        // The shape of a redelivered announcement: after the first pass the absorbed identifier is
        // on no claim here, and the second pass must not deduplicate the survivor's own claims —
        // one queued and one awaiting pickup is a state a merge legitimately leaves behind.
        var queue = AQueue();
        var surviving = BorrowerId.Generate();
        ClaimOf(queue, surviving, hoursIn: 0);
        ClaimOf(queue, BorrowerId.Generate(), hoursIn: 1);
        queue.Settled();

        queue.CombineClaimsOf(BorrowerId.Generate(), surviving);

        queue.Holds.Count.ShouldBe(2);
        queue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void CombineClaimsOf_TheSameRecordOnBothSides_RecordsNothing()
    {
        // Members refuses a self-merge before anything is announced, and the validator refuses the
        // command's shape; a queue reached all the same must not report claims moving to where
        // they already are.
        var queue = AQueue();
        var borrower = BorrowerId.Generate();
        ClaimOf(queue, borrower);
        queue.Settled();

        queue.CombineClaimsOf(borrower, borrower);

        queue.Holds.ShouldHaveSingleItem().BorrowerId.ShouldBe(borrower);
        queue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void CombineClaimsOf_DemandsBothRecords()
    {
        var queue = AQueue();

        Should.Throw<ArgumentNullException>(
            () => queue.CombineClaimsOf(null!, BorrowerId.Generate()));
        Should.Throw<ArgumentNullException>(
            () => queue.CombineClaimsOf(BorrowerId.Generate(), null!));
    }
}
