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
/// <param name="balances">The port Charges answers — the truth the trigger is checked against,
/// and the source of the offer that follows a released copy.</param>
/// <param name="policy">The circulation policy — the pickup period, what a debt forbids.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// <para>
/// <strong>The event is the trigger; the live balance is the truth.</strong> Between the fine and
/// this handler lies the drain — a minute ordinarily, longer behind a blocked head — and the most
/// ordinary act at a desk is paying. A borrower who cleared their debt inside that window must
/// not lose months of queue position to a message about a balance that no longer exists, and the
/// cancellation is irreversible by design. So the port is read again before anything goes, and a
/// debt already repaid cancels nothing.
/// </para>
/// <para>
/// The claim awaiting pickup releases its copy, and that copy is offered to the next borrower in
/// good standing rather than left on the hold shelf: a trapped copy for a newly blocked borrower
/// is precisely the waste the rule exists to prevent.
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

        var stillBlocked = await Standing.IsBlockedAsync(
                borrowerId, balances, policy, cancellationToken)
            .ConfigureAwait(false);

        if (!stillBlocked)
        {
            return Result.Success();
        }

        await DebtCancellation.CancelEveryHoldOfAsync(
                borrowerId, queues, balances, policy, clock.Today(), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
