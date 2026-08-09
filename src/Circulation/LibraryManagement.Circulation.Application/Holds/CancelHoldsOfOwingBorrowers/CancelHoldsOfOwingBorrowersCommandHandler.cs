using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelHoldsOfOwingBorrowers;

/// <summary>
/// Handles <see cref="CancelHoldsOfOwingBorrowersCommand"/>.
/// </summary>
/// <param name="queues">Every queue holding live claims — the sweep starts from the borrowers
/// they name.</param>
/// <param name="balances">The port Charges answers, one question per borrower.</param>
/// <param name="policy">The circulation policy — the pickup period, what a debt forbids.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// <para>
/// One save for the whole sweep — the grain the expiry already set: every borrower judged is
/// judged against the same instant, and a sweep that half-committed would leave the queues in
/// exactly the state it exists to repair.
/// </para>
/// <para>
/// Idempotent by state rather than by memory: a borrower cancelled today holds nothing tomorrow,
/// so running the sweep twice finds nothing the second time.
/// </para>
/// </remarks>
public sealed class CancelHoldsOfOwingBorrowersCommandHandler(
    IHoldQueueRepository queues,
    IMemberBalance balances,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<CancelHoldsOfOwingBorrowersCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CancelHoldsOfOwingBorrowersCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var today = clock.Today();

        var borrowers = await queues.BorrowersWithLiveHoldsAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var borrowerId in borrowers)
        {
            var blocked = await Standing.IsBlockedAsync(
                    borrowerId, balances, policy, cancellationToken)
                .ConfigureAwait(false);

            if (blocked)
            {
                await DebtCancellation.CancelEveryHoldOfAsync(
                        borrowerId, queues, balances, policy, today, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return Result.Success();
    }
}
