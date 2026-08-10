using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Catalog.PublishedLanguage;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Members.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.PlaceHold;

/// <summary>
/// Handles <see cref="PlaceHoldCommand"/>.
/// </summary>
/// <param name="loans">This context's loans — for the cap, and for the copies already out.</param>
/// <param name="queues">The queue the claim joins, opened by its first claim if need be.</param>
/// <param name="editions">Catalog's published language: does this identifier still name a record.</param>
/// <param name="copies">Holdings' published language: which copies of the edition could be lent.</param>
/// <param name="members">Members' published language: is this person entitled to borrow.</param>
/// <param name="balances">The port Circulation declared for Charges.</param>
/// <param name="policy">The circulation policy — the cap, what a debt forbids.</param>
/// <param name="clock">The host's clock. The placement instant is the position, forever.</param>
/// <remarks>
/// The shelf refusal is the availability arithmetic run once, on the write path: Holdings says
/// which copies could be lent, this context subtracts the ones out on loan and the ones set aside
/// on the hold shelf, and anything left is a copy the borrower should simply take to the desk —
/// allowing the hold would make the queue meaningless, and this refusal and the renewal rule
/// protect each other.
/// </remarks>
public sealed class PlaceHoldCommandHandler(
    ILoanRepository loans,
    IHoldQueueRepository queues,
    IEditionCatalog editions,
    ICopyLendability copies,
    IMemberEntitlement members,
    IMemberBalance balances,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<PlaceHoldCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        PlaceHoldCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var borrowerId = BorrowerId.Create(command.BorrowerId);
        var editionId = EditionId.Create(command.EditionId);

        // Asked first, because it is the cheapest question and the one that invalidates every
        // other. It is also the answer to a mistake nothing else here can see: Catalog stops
        // acknowledging an identifier it has merged away, so a claim placed on a stale screen would
        // otherwise open a queue no return ever feeds — the copies of that record were refiled
        // under its survivor, so even the shelf check below goes quiet.
        if (!await editions.ExistsAsync(command.EditionId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(
                CirculationErrorCodes.NoSuchEdition,
                $"No edition answers to '{editionId}' — a mistyped identifier, or a record a "
                + "cataloger has merged away. Search again and claim the record that answers.");
        }

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
                "The borrower owes money, and a debt forbids placing a hold.");
        }

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

        var alreadyBorrowed = await loans
            .BorrowerHasActiveLoanForEditionAsync(borrowerId, editionId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyBorrowed)
        {
            return Result.Failure(
                CirculationErrorCodes.AlreadyBorrowed,
                "The borrower already has a copy of this edition on loan; a hold would claim "
                + "what they hold.");
        }

        var queue = await queues.GetByEditionAsync(editionId, cancellationToken)
            .ConfigureAwait(false);

        var onTheShelf = await ACopyIsOnTheShelfAsync(command.EditionId, queue, cancellationToken)
            .ConfigureAwait(false);

        if (onTheShelf)
        {
            return Result.Failure(
                CirculationErrorCodes.CopyOnTheShelf,
                "A copy is available on the shelf — that is a checkout, not a hold.");
        }

        if (queue is null)
        {
            queue = HoldQueue.For(editionId);
            queues.Add(queue);
        }

        return queue.PlaceHold(
            HoldId.Create(command.HoldId),
            borrowerId,
            clock.GetUtcNow());
    }

    private async ValueTask<bool> ACopyIsOnTheShelfAsync(
        Guid editionId,
        HoldQueue? queue,
        CancellationToken cancellationToken)
    {
        var lendable = await copies.LendableCopiesOfAsync(editionId, cancellationToken)
            .ConfigureAwait(false);

        if (lendable.Count == 0)
        {
            return false;
        }

        var candidates = lendable.Select(CopyId.Create).ToList();

        var outOnLoan = await loans.OnActiveLoanAmongAsync(candidates, cancellationToken)
            .ConfigureAwait(false);

        var trapped = queue?.TrappedCopyIds() ?? [];

        return candidates.Any(copy => !outOnLoan.Contains(copy) && !trapped.Contains(copy));
    }
}
