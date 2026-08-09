using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Members.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using IHoldQueueRepository = LibraryManagement.Circulation.Domain.Holds.IHoldQueueRepository;

namespace LibraryManagement.Circulation.Application.Loans.RenewLoan;

/// <summary>
/// Handles <see cref="RenewLoanCommand"/>.
/// </summary>
/// <param name="loans">The loan to renew.</param>
/// <param name="queues">The queue whose emptiness is what makes a queue move: without this check
/// a borrower renews indefinitely and the people behind them never get anything.</param>
/// <param name="members">Members' published language: is this person still entitled to borrow.
/// A renewal is a fresh loan period, not the tail of an old one, so it opens with the question
/// every granting act opens with — without it, an expired membership could no longer borrow but
/// could renew indefinitely, an asymmetry nothing had decided.</param>
/// <param name="balances">The port Circulation declared for Charges.</param>
/// <param name="policy">The circulation policy — the limit, the extension, what a debt forbids.</param>
/// <remarks>
/// The rule is blunt on purpose: any queued claim denies every renewal of the edition, because
/// the refined version has to choose <em>which</em> borrowers are denied, and any answer is
/// arbitrary and unexplainable at the desk. A claim already awaiting pickup does not deny — its
/// copy is on the hold shelf, and refusing a renewal for its sake would serve nobody.
/// </remarks>
public sealed class RenewLoanCommandHandler(
    ILoanRepository loans,
    IHoldQueueRepository queues,
    IMemberEntitlement members,
    IMemberBalance balances,
    CirculationPolicy policy) : ICommandHandler<RenewLoanCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RenewLoanCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loanId = LoanId.Create(command.LoanId);
        var loan = await loans.GetByIdAsync(loanId, cancellationToken).ConfigureAwait(false);

        if (loan is null)
        {
            return Result.Failure(
                CirculationErrorCodes.LoanNotFound,
                $"No loan is held under '{loanId}'.");
        }

        // A loan that is not active is refused by the aggregate below; asking the ports about it
        // first would report a debt to someone whose loan is simply over.
        if (loan.Status == LoanStatus.Active)
        {
            var entitlement = await members.OfAsync(loan.BorrowerId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (entitlement.Entitlement == Entitlement.NoSuchMember)
            {
                return Result.Failure(
                    CirculationErrorCodes.NoSuchMember,
                    $"Nobody is enrolled under '{loan.BorrowerId}' — the registry no longer "
                    + "answers for this borrower.");
            }

            if (entitlement.Entitlement == Entitlement.Lapsed)
            {
                return Result.Failure(
                    CirculationErrorCodes.MembershipLapsed,
                    "The membership has lapsed; renewing it comes before renewing the loan.");
            }

            if (await Standing.IsBlockedAsync(loan.BorrowerId, balances, policy, cancellationToken)
                    .ConfigureAwait(false))
            {
                return Result.Failure(
                    CirculationErrorCodes.DebtForbidsIt,
                    "The borrower owes money, and a debt forbids renewing.");
            }

            var queue = await queues.GetByEditionAsync(loan.EditionId, cancellationToken)
                .ConfigureAwait(false);

            if (queue is not null && queue.AnyoneIsWaiting)
            {
                return Result.Failure(
                    CirculationErrorCodes.SomeoneIsWaiting,
                    "Someone is waiting for this edition; please bring the copy back.");
            }
        }

        return loan.Renew(policy);
    }
}
