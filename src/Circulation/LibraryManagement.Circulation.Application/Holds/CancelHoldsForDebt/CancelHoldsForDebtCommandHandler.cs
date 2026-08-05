using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelHoldsForDebt;

/// <summary>
/// Handles <see cref="CancelHoldsForDebtCommand"/>.
/// </summary>
/// <param name="queues">Every queue the borrower occupies a place in.</param>
/// <param name="balances">The port Charges answers, for the offer that follows a released copy.</param>
/// <param name="policy">The circulation policy — the pickup period, what a debt forbids.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// <para>
/// Step two of the design's three is the one worth naming: a claim awaiting pickup releases the copy
/// set aside for it, and that copy is offered to the next borrower in good standing rather than
/// left on the hold shelf. A trapped copy for a newly blocked borrower is precisely the waste the
/// rule exists to prevent, and leaving it there until its deadline would reintroduce it.
/// </para>
/// <para>
/// <strong>It succeeds when there is nothing to cancel.</strong> A borrower who owes money and holds
/// no claims is the ordinary case, and this arrives from another module's drain — a refusal would
/// make that drain replay a message that was handled correctly, forever.
/// </para>
/// </remarks>
public sealed class CancelHoldsForDebtCommandHandler(
    IHoldQueueRepository queues,
    IMemberBalance balances,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<CancelHoldsForDebtCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CancelHoldsForDebtCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var borrowerId = BorrowerId.Create(command.BorrowerId);
        var today = clock.Today();

        var occupied = await queues.WithHoldsForBorrowerAsync(borrowerId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var queue in occupied)
        {
            var cancellation = queue.CancelForDebt(borrowerId);

            if (cancellation?.ReleasedCopyId is null || !queue.AnyoneIsWaiting)
            {
                continue;
            }

            // The borrower who just lost their place is gone from the queue, so this asks about
            // whoever is left — and it asks Charges again, because the debt that triggered all this
            // may not be theirs alone.
            var blocked = await Standing.BlockedAmongAsync(
                    queue.QueuedBorrowersInOrder(), balances, policy, cancellationToken)
                .ConfigureAwait(false);

            queue.TrapOldestQueued(
                cancellation.ReleasedCopyId,
                today.AddDays(policy.PickupPeriodInDays),
                blocked);
        }

        return Result.Success();
    }
}
