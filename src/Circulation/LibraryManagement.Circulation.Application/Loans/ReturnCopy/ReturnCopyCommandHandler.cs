using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using IHoldQueueRepository = LibraryManagement.Circulation.Domain.Holds.IHoldQueueRepository;

namespace LibraryManagement.Circulation.Application.Loans.ReturnCopy;

/// <summary>
/// Handles <see cref="ReturnCopyCommand"/>.
/// </summary>
/// <param name="loans">The loan that closes.</param>
/// <param name="queues">The queue that may want the copy.</param>
/// <param name="balances">The port Circulation declared for Charges, consulted per queued
/// borrower so a blocked one is skipped — never removed; the debt event may simply not have
/// arrived yet, and the removal is that handler's job.</param>
/// <param name="policy">The circulation policy — the pickup period, what a debt forbids.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// <strong>Two aggregates change together, and that is deliberate.</strong> A copy shelved that
/// was promised, or a hold announced ready for a copy nobody set aside, are both visible at the
/// desk, to the member — so the loan and the queue agree at every instant, in one save, inside
/// one context. Both change here, on the aggregates, directly, never across an event handler: an
/// event is always handled later, and an invariant that waits is not an invariant.
/// </remarks>
public sealed class ReturnCopyCommandHandler(
    ILoanRepository loans,
    IHoldQueueRepository queues,
    IMemberBalance balances,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<ReturnCopyCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        ReturnCopyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var copyId = CopyId.Create(command.CopyId);

        var loan = await loans.ActiveLoanForCopyAsync(copyId, cancellationToken)
            .ConfigureAwait(false);

        if (loan is null)
        {
            return Result.Failure(
                CirculationErrorCodes.NothingOnLoan,
                $"No active loan carries copy '{copyId}' — nothing is out, so nothing can come back.");
        }

        var today = clock.Today();

        var returned = loan.Return(today, policy);

        if (returned.HasErrors())
        {
            return returned;
        }

        var queue = await queues.GetByEditionAsync(loan.EditionId, cancellationToken)
            .ConfigureAwait(false);

        if (queue is not null && queue.AnyoneIsWaiting)
        {
            var blocked = await Standing.BlockedAmongAsync(
                    queue.QueuedBorrowersInOrder(), balances, policy, cancellationToken)
                .ConfigureAwait(false);

            // Null when every queued borrower is blocked: the copy goes back to the shelf, and
            // nothing is recorded — Holdings never learns a return happened, by design.
            queue.TrapOldestQueued(copyId, policy.PickupDeadlineFor(today), blocked);
        }

        return Result.Success();
    }
}
