using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.PublishedLanguage;

namespace LibraryManagement.Circulation.Application;

/// <summary>
/// The judgement the glossary reserves the word for: whether what a borrower owes forbids the act.
/// Charges states an amount; this is where the amount becomes a verdict.
/// </summary>
/// <remarks>
/// Public rather than internal since the borrower's file began displaying the verdict. The read
/// side lives in this module's infrastructure, a second assembly, and the alternative was for its
/// handler to fetch the amount and apply <c>DebtForbids</c> itself — two lines, and the judgement
/// in two places. What makes that worse than the widened surface is the direction the copies would
/// drift: the day a debt stops being the only thing that blocks a borrower, the desk paths and the
/// screen would disagree about the same borrower, and only one of them would be tested.
/// </remarks>
public static class Standing
{
    /// <summary>
    /// Judges one borrower at the desk.
    /// </summary>
    /// <param name="borrowerId">The borrower asking to act.</param>
    /// <param name="balances">Charges' answer, through the port Circulation declared.</param>
    /// <param name="policy">The policy holding what a debt forbids.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when a debt blocks borrowing, renewing and placing holds.</returns>
    /// <remarks>
    /// The only member the module exports. Judging a whole queue is desk machinery and stays
    /// internal; judging one borrower is what a file displays.
    /// </remarks>
    public static async ValueTask<bool> IsBlockedAsync(
        BorrowerId borrowerId,
        IMemberBalance balances,
        CirculationPolicy policy,
        CancellationToken cancellationToken)
    {
        var owed = await balances.OwedByAsync(borrowerId.Value, cancellationToken)
            .ConfigureAwait(false);

        return policy.DebtForbids(owed);
    }

    /// <summary>
    /// Judges a queue's worth of borrowers, for the moment a copy is offered to the oldest claim
    /// whose borrower is in good standing.
    /// </summary>
    /// <param name="borrowers">The queued borrowers, in any order.</param>
    /// <param name="balances">Charges' answer, per borrower.</param>
    /// <param name="policy">The policy holding what a debt forbids.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The borrowers a debt blocks — the ones the queue will skip, never remove: the
    /// debt event may simply not have arrived yet, and the removal is that handler's job.</returns>
    /// <remarks>
    /// One question per borrower, sequentially: queues are a few people long at library scale,
    /// and the scoped context behind the port is not thread-safe.
    /// </remarks>
    internal static async ValueTask<IReadOnlySet<BorrowerId>> BlockedAmongAsync(
        IEnumerable<BorrowerId> borrowers,
        IMemberBalance balances,
        CirculationPolicy policy,
        CancellationToken cancellationToken)
    {
        var blocked = new HashSet<BorrowerId>();

        foreach (var borrowerId in borrowers.Distinct())
        {
            if (await IsBlockedAsync(borrowerId, balances, policy, cancellationToken)
                    .ConfigureAwait(false))
            {
                blocked.Add(borrowerId);
            }
        }

        return blocked;
    }
}
