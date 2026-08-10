using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using static LibraryManagement.Circulation.Domain.Tests.Desk;

namespace LibraryManagement.Circulation.Domain.Tests.Holds;

/// <summary>
/// What survives when two queues become one — the expensive half of a merge, and the one ADR-0017
/// was written for.
/// </summary>
/// <remarks>
/// The concrete type and not the abstraction: the interface exists so the handler can be given a
/// collaborator, not so this can be faked. There is nothing to simulate in logic that reaches no
/// store, and a stub here would assert the stub.
/// </remarks>
public sealed class HoldQueueMergeDomainServiceTests
{
    private static readonly IReadOnlySet<BorrowerId> NobodyBlocked = new HashSet<BorrowerId>();

    private readonly HoldQueueMergeDomainService _merge = new();

    private static DateOnly Deadline => Today.AddDays(Policy.PickupPeriodInDays);

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    private static HoldId AClaimBy(HoldQueue queue, BorrowerId borrowerId, int hoursIn)
    {
        var holdId = HoldId.Generate();
        queue.PlaceHold(holdId, borrowerId, ThisMorning.AddHours(hoursIn));
        return holdId;
    }

    [Fact]
    public void Merge_LeavesTheAbsorbedQueueEmpty()
    {
        var absorbed = AQueue();
        var surviving = AQueue();
        AClaimBy(absorbed, BorrowerId.Generate(), 0);

        _merge.Merge(absorbed, surviving).HasErrors().ShouldBeFalse();

        absorbed.Holds.ShouldBeEmpty();
        surviving.Holds.Count.ShouldBe(1);
    }

    [Fact]
    public void Merge_InterleavesTheTwoQueuesByPlacementInstant()
    {
        // Order needs no rule: a queue is served by placement instant and the union carries every
        // instant with its claim, so the two interleave by construction.
        var absorbed = AQueue();
        var surviving = AQueue();
        var first = BorrowerId.Generate();
        var second = BorrowerId.Generate();
        var third = BorrowerId.Generate();
        AClaimBy(surviving, first, 0);
        AClaimBy(absorbed, second, 1);
        AClaimBy(surviving, third, 2);

        _merge.Merge(absorbed, surviving);

        surviving.QueuedBorrowersInOrder().ShouldBe([first, second, third]);
    }

    [Fact]
    public void Merge_SaysWhichQueueEachClaimCameFrom()
    {
        var absorbed = AQueue();
        var surviving = AQueue();
        var holdId = AClaimBy(absorbed, BorrowerId.Generate(), 0);
        surviving.ClearDomainEvents();

        _merge.Merge(absorbed, surviving);

        var moved = surviving.Event<HoldMovedToMergedQueue>();
        moved.HoldId.ShouldBe(holdId);
        moved.EditionId.ShouldBe(surviving.Id);
        moved.PreviousEditionId.ShouldBe(absorbed.Id);
    }

    [Fact]
    public void Merge_ABorrowersLaterQueuedClaim_GivesWayToTheirEarlier()
    {
        // Their claim on the work is as old as the first time they asked for it; the later one was
        // a claim on what turns out to be the same thing.
        var borrowerId = BorrowerId.Generate();
        var absorbed = AQueue();
        var surviving = AQueue();
        var earliest = AClaimBy(surviving, borrowerId, 0);
        var later = AClaimBy(absorbed, borrowerId, 1);
        surviving.ClearDomainEvents();

        _merge.Merge(absorbed, surviving);

        surviving.Holds.ShouldHaveSingleItem().Id.ShouldBe(earliest);

        var superseded = surviving.Event<HoldCancelledAsDuplicate>();
        superseded.HoldId.ShouldBe(later);
        superseded.SurvivingHoldId.ShouldBe(earliest);
        superseded.BorrowerId.ShouldBe(borrowerId);
    }

    [Fact]
    public void Merge_TheEarlierClaimKeepsItsPlaceAmongTheOthers()
    {
        // Nothing is lost, and this is what that means concretely: the borrower waits from the
        // first time they asked, so somebody who queued in between stays behind them.
        var borrowerId = BorrowerId.Generate();
        var between = BorrowerId.Generate();
        var absorbed = AQueue();
        var surviving = AQueue();
        AClaimBy(surviving, borrowerId, 0);
        AClaimBy(absorbed, between, 1);
        AClaimBy(absorbed, borrowerId, 2);

        _merge.Merge(absorbed, surviving);

        surviving.QueuedBorrowersInOrder().ShouldBe([borrowerId, between]);
    }

    [Fact]
    public void Merge_AClaimAwaitingPickup_AlwaysSurvives()
    {
        // A copy is physically on the hold shelf with somebody's name on it. Dropping the claim
        // strands the copy and breaks a promise the library already made in the world — which is
        // why cancelling the later claim outright was rejected, invariant or no invariant.
        var borrowerId = BorrowerId.Generate();
        var absorbed = AQueue();
        var surviving = AQueue();
        var queued = AClaimBy(surviving, borrowerId, 0);
        var trapped = AClaimBy(absorbed, borrowerId, 1);
        var copyId = CopyId.Generate();
        absorbed.TrapOldestQueued(copyId, Deadline, NobodyBlocked);
        surviving.ClearDomainEvents();

        _merge.Merge(absorbed, surviving).HasErrors().ShouldBeFalse();

        // Both stand: the invariant only ever spoke about queued claims, and the merge is what
        // revealed it was overstated.
        surviving.Holds.Select(hold => hold.Id).ShouldBe([queued, trapped], ignoreOrder: true);
        surviving.TrappedCopyIds().ShouldBe([copyId]);
        surviving.DomainEvents.OfType<HoldCancelledAsDuplicate>().ShouldBeEmpty();
    }

    [Fact]
    public void Merge_TheRareDoubleTrap_KeepsBothCopiesSetAside()
    {
        var borrowerId = BorrowerId.Generate();
        var absorbed = AQueue();
        var surviving = AQueue();
        AClaimBy(surviving, borrowerId, 0);
        AClaimBy(absorbed, borrowerId, 1);
        var here = CopyId.Generate();
        var there = CopyId.Generate();
        surviving.TrapOldestQueued(here, Deadline, NobodyBlocked);
        absorbed.TrapOldestQueued(there, Deadline, NobodyBlocked);

        _merge.Merge(absorbed, surviving);

        // The store agrees: the unique index on the trapped copy is filtered and spans queues, so
        // two different trapped copies in one queue violate nothing.
        surviving.TrappedCopyIds().ShouldBe([here, there], ignoreOrder: true);
    }

    [Fact]
    public void Merge_LeavesTwoDifferentBorrowersAlone()
    {
        var absorbed = AQueue();
        var surviving = AQueue();
        AClaimBy(surviving, BorrowerId.Generate(), 0);
        AClaimBy(absorbed, BorrowerId.Generate(), 1);
        surviving.ClearDomainEvents();

        _merge.Merge(absorbed, surviving);

        surviving.Holds.Count.ShouldBe(2);
        surviving.DomainEvents.OfType<HoldCancelledAsDuplicate>().ShouldBeEmpty();
    }

    [Fact]
    public void Merge_ThreeQueuedClaimsOfOneBorrower_LeaveTheEarliest()
    {
        // Two merges in a row can reach this, and the rule is the same one applied to the set.
        var borrowerId = BorrowerId.Generate();
        var absorbed = AQueue();
        var surviving = AQueue();
        var earliest = AClaimBy(surviving, borrowerId, 0);
        AClaimBy(absorbed, borrowerId, 1);
        AClaimBy(absorbed, borrowerId, 2);

        _merge.Merge(absorbed, surviving);

        surviving.Holds.ShouldHaveSingleItem().Id.ShouldBe(earliest);
        surviving.DomainEvents.OfType<HoldCancelledAsDuplicate>()
            .ShouldAllBe(superseded => superseded.SurvivingHoldId == earliest);
    }

    [Fact]
    public void Merge_AQueueIntoItself_IsRefused()
    {
        var queue = AQueue();

        CodesOf(_merge.Merge(queue, queue))
            .ShouldContain(CirculationErrorCodes.QueueCannotAbsorbItself);
    }

    [Fact]
    public void Merge_AnEmptyAbsorbedQueue_ChangesNothing()
    {
        var absorbed = AQueue();
        var surviving = AQueue();
        AClaimBy(surviving, BorrowerId.Generate(), 0);
        surviving.ClearDomainEvents();

        _merge.Merge(absorbed, surviving).HasErrors().ShouldBeFalse();

        surviving.Holds.Count.ShouldBe(1);
        surviving.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_DemandsBothQueues()
    {
        Should.Throw<ArgumentNullException>(() => _merge.Merge(null!, AQueue()));
        Should.Throw<ArgumentNullException>(() => _merge.Merge(AQueue(), null!));
    }
}
