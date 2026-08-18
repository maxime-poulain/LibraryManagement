using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Domain.Holds;

/// <summary>
/// The rules that decide which claims survive when two queues become one, and the act of joining
/// them.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The expensive half of a merge, and the reason ADR-0017 exists.</strong> Holdings repoints
/// a column and this context repoints a loan's field; here the identifier is the queue's own
/// <em>key</em>, so nothing is repointed — two aggregates become one, against an invariant the queue
/// refuses to create on its own.
/// </para>
/// <para>
/// <strong>Order needs no rule.</strong> A queue is served by placement instant and the union
/// carries every instant with its claim, so two queues interleave correctly by construction. Only
/// the collisions need deciding, and there are two kinds.
/// </para>
/// <para>
/// <strong>A claim awaiting pickup always survives.</strong> A copy is physically on the hold shelf
/// with somebody's name on it. Dropping such a claim strands the copy and breaks a promise the
/// library already made in the world — which is why the alternative of cancelling the later claim
/// outright, invariant intact, was rejected.
/// </para>
/// <para>
/// <strong>Among a borrower's queued claims, only the earliest survives.</strong> Their claim on the
/// work is as old as the first time they asked for it; the later one was a claim on what turns out
/// to be the same thing. Nothing is lost, and the event says which claim remains so a reader can see
/// that. This rule no longer lives here: the member merge asks the same question of one queue alone,
/// which showed it had always been about one aggregate's own claims — it is
/// <c>HoldQueue.KeepEarliestQueuedClaimOf</c>, and this service invokes it for every borrower the
/// union may have doubled.
/// </para>
/// <para>
/// So the invariant is restated rather than abandoned: <em>at most one queued claim per borrower</em>.
/// It never had to speak about claims a copy is already set aside for, and the merge is what reveals
/// it was overstated — a borrower may legitimately end up with one queued claim and one copy on the
/// hold shelf, or in the rare double trap, two copies. The store agrees: the unique index on the
/// trapped copy is filtered and spans queues, so two <em>different</em> trapped copies in one queue
/// violate nothing.
/// </para>
/// <para>
/// Nothing here reaches a store, which is the property that made it a domain service rather than a
/// handler; and what remains here is exactly what concerns the pair rather than either queue —
/// whether they may be joined, and that the union is read against the invariant afterwards.
/// </para>
/// </remarks>
public sealed class HoldQueueMergeDomainService : IHoldQueueMergeDomainService
{
    /// <inheritdoc/>
    public Result Merge(HoldQueue absorbed, HoldQueue surviving)
    {
        ArgumentNullException.ThrowIfNull(absorbed);
        ArgumentNullException.ThrowIfNull(surviving);

        if (absorbed.Id == surviving.Id)
        {
            return Result.Failure(
                CirculationErrorCodes.QueueCannotAbsorbItself,
                "A hold queue cannot be merged into itself.");
        }

        surviving.AbsorbHoldsFrom(absorbed);

        // Per borrower rather than in one sweep so the event can name the surviving claim, which
        // is the whole difference between "your hold was cancelled" and "your two requests were
        // one".
        foreach (var borrower in surviving.Holds.Select(hold => hold.BorrowerId).Distinct().ToList())
        {
            surviving.KeepEarliestQueuedClaimOf(borrower);
        }

        return Result.Success();
    }
}
