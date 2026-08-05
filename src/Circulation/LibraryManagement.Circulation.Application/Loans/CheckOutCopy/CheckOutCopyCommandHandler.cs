using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Members.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using IHoldQueueRepository = LibraryManagement.Circulation.Domain.Holds.IHoldQueueRepository;

namespace LibraryManagement.Circulation.Application.Loans.CheckOutCopy;

/// <summary>
/// Handles <see cref="CheckOutCopyCommand"/> — the moment three contexts answer for one act.
/// </summary>
/// <param name="loans">This context's loans: the copy must not already be out.</param>
/// <param name="queues">This context's queues: a trapped copy goes only to its claimant.</param>
/// <param name="copies">Holdings' published language: may this copy be lent, and of what edition.</param>
/// <param name="members">Members' published language: is this person entitled to borrow.</param>
/// <param name="balances">The port Circulation declared for Charges: what does this person owe.</param>
/// <param name="policy">The circulation policy — the cap, the duration, what a debt forbids.</param>
/// <param name="clock">The host's clock. The domain has no today of its own.</param>
/// <remarks>
/// <para>
/// Preconditions run cheapest and most-likely-to-fail first, each refusal naming what the
/// librarian tells the member. The design's list opens with standing; entitlement runs before it,
/// because a person the registry does not know cannot be judged for debt — the amendment the
/// strategic design always implied and the tactical design now records.
/// </para>
/// <para>
/// The cap is checked only when the checkout <em>adds</em>: collecting one's own trapped hold
/// turns a claim into a loan and leaves the count unchanged, so nothing is reconciled at pickup —
/// which moves the cap after the trap resolution, the one departure from the listed order and the
/// design's own §2 is the reason.
/// </para>
/// </remarks>
public sealed class CheckOutCopyCommandHandler(
    ILoanRepository loans,
    IHoldQueueRepository queues,
    ICopyLendability copies,
    IMemberEntitlement members,
    IMemberBalance balances,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<CheckOutCopyCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CheckOutCopyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var borrowerId = BorrowerId.Create(command.BorrowerId);
        var copyId = CopyId.Create(command.CopyId);

        var entitlement = await members.OfAsync(command.BorrowerId, cancellationToken)
            .ConfigureAwait(false);

        if (entitlement.Entitlement == Entitlement.NoSuchMember)
        {
            return Result.Failure(
                CirculationErrorCodes.NoSuchMember,
                $"Nobody is enrolled under '{borrowerId}' — a mis-scan, or another library's card.");
        }

        if (entitlement.Entitlement == Entitlement.Lapsed)
        {
            return Result.Failure(
                CirculationErrorCodes.MembershipLapsed,
                "The membership has lapsed; the conversation to have is a renewal.");
        }

        if (await Standing.IsBlockedAsync(borrowerId, balances, policy, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure(
                CirculationErrorCodes.DebtForbidsIt,
                "The borrower owes money, and a debt forbids borrowing.");
        }

        var lendability = await copies.OfAsync(command.CopyId, cancellationToken)
            .ConfigureAwait(false);

        if (lendability.Lendability == Lendability.NoSuchCopy)
        {
            return Result.Failure(
                CirculationErrorCodes.NoSuchCopy,
                $"No copy is held under '{copyId}' — a mis-scan, or a copy never accessioned.");
        }

        if (lendability.Lendability == Lendability.NotLendable)
        {
            return Result.Failure(
                CirculationErrorCodes.CopyNotLendable,
                "Holdings will not lend this copy — in repair, reference-only, lost or withdrawn.");
        }

        var editionId = EditionId.Create(lendability.EditionId!.Value);

        var alreadyOut = await loans.ActiveLoanForCopyAsync(copyId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyOut is not null)
        {
            return Result.Failure(
                CirculationErrorCodes.CopyAlreadyOnLoan,
                "This copy is already out on loan; it has to come back before it goes out again.");
        }

        var queue = await queues.GetByEditionAsync(editionId, cancellationToken)
            .ConfigureAwait(false);

        // What stops a walk-in from being handed a copy someone is waiting for.
        var trappedFor = queue?.HoldAwaitingPickupOf(copyId);

        if (trappedFor is not null && trappedFor.BorrowerId != borrowerId)
        {
            return Result.Failure(
                CirculationErrorCodes.TrappedForAnotherBorrower,
                "This copy is set aside for another borrower's hold.");
        }

        var fulfillsOwnHold = trappedFor is not null;

        if (!fulfillsOwnHold)
        {
            var load = await loans.CountActiveForBorrowerAsync(borrowerId, cancellationToken)
                    .ConfigureAwait(false)
                + await queues.CountLiveForBorrowerAsync(borrowerId, cancellationToken)
                    .ConfigureAwait(false);

            if (policy.IsAtTheCap(load))
            {
                return Result.Failure(
                    CirculationErrorCodes.AtTheCap,
                    $"Loans and holds together are at the cap of {policy.MaxLoansAndHolds}; "
                    + "something has to come back first.");
            }
        }

        if (fulfillsOwnHold)
        {
            var fulfilled = queue!.Fulfill(copyId);

            if (fulfilled.HasErrors())
            {
                return fulfilled;
            }
        }

        loans.Add(Loan.CheckOut(
            LoanId.Create(command.LoanId),
            copyId,
            editionId,
            borrowerId,
            clock.Today(),
            policy));

        return Result.Success();
    }
}
