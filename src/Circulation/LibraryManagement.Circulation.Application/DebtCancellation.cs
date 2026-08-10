using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.PublishedLanguage;

namespace LibraryManagement.Circulation.Application;

/// <summary>
/// The one routine both debt moments share: every place a borrower occupies goes, and every copy
/// that was set aside for them is offered on.
/// </summary>
/// <remarks>
/// Two callers, one act. The debt event's handler runs it the day the news arrives; the scheduled
/// reconciliation runs it for the borrower whose news never did. Extracted rather than duplicated
/// because the second caller may not dispatch the first — a command handler never depends on a
/// dispatcher — and a cancellation that diverged between the two roads would be a rule with two
/// behaviors.
/// </remarks>
internal static class DebtCancellation
{
    /// <summary>
    /// Cancels every live claim of one borrower, releasing and re-offering what was set aside.
    /// </summary>
    /// <param name="borrowerId">The borrower whose debt forbids holding a place.</param>
    /// <param name="queues">Every queue they occupy.</param>
    /// <param name="balances">The port Charges answers, for the offer that follows a released
    /// copy.</param>
    /// <param name="policy">The circulation policy — the pickup period, what a debt forbids.</param>
    /// <param name="today">The day of the act, which starts any new pickup countdown.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <remarks>
    /// The offer that follows a release asks Charges again, about whoever is left: the debt that
    /// triggered all this may not be the leaver's alone, and a copy set aside for the next owing
    /// borrower would be the exact waste the rule exists to prevent.
    /// </remarks>
    internal static async ValueTask CancelEveryHoldOfAsync(
        BorrowerId borrowerId,
        IHoldQueueRepository queues,
        IMemberBalance balances,
        CirculationPolicy policy,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var occupied = await queues.WithHoldsForBorrowerAsync(borrowerId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var queue in occupied)
        {
            // Every claim of theirs in this queue, not the first: a merged queue may hold two of
            // one borrower's, and leaving one behind would leave somebody who owes money occupying
            // a place — exactly the invariant this rule exists to buy.
            var cancellations = queue.CancelForDebt(borrowerId);

            foreach (var released in cancellations
                         .Select(cancellation => cancellation.ReleasedCopyId)
                         .OfType<CopyId>())
            {
                if (!queue.AnyoneIsWaiting)
                {
                    break;
                }

                // Asked again per copy: the previous offer may have taken the only borrower in good
                // standing, and the answer is about whoever is left.
                var blocked = await Standing.BlockedAmongAsync(
                        queue.QueuedBorrowersInOrder(), balances, policy, cancellationToken)
                    .ConfigureAwait(false);

                queue.TrapOldestQueued(released, policy.PickupDeadlineFor(today), blocked);
            }
        }
    }
}
