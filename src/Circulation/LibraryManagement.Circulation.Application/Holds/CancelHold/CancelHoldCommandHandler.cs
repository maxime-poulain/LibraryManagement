using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelHold;

/// <summary>
/// Handles <see cref="CancelHoldCommand"/>.
/// </summary>
/// <param name="queues">The queue the claim leaves.</param>
/// <param name="balances">The port Circulation declared for Charges, for the offer that follows
/// a released copy.</param>
/// <param name="policy">The circulation policy — the pickup period, what a debt forbids.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// A cancelled claim that was awaiting pickup releases its trapped copy back to the queue, where
/// it is offered to the next borrower in good standing — exactly as an expiry will release it,
/// when the scheduled process exists. The offer happens here, in the same save as the
/// cancellation, because a copy on the hold shelf that no live claim owns is a promise to nobody.
/// </remarks>
public sealed class CancelHoldCommandHandler(
    IHoldQueueRepository queues,
    IMemberBalance balances,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<CancelHoldCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CancelHoldCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var editionId = EditionId.Create(command.EditionId);
        var borrowerId = BorrowerId.Create(command.BorrowerId);
        var holdId = HoldId.Create(command.HoldId);

        var queue = await queues.GetByEditionAsync(editionId, cancellationToken)
            .ConfigureAwait(false);

        if (queue is null)
        {
            return Result.Failure(
                CirculationErrorCodes.NoSuchHold,
                "No hold of this borrower waits on this edition under that identifier.");
        }

        var cancelled = queue.CancelFor(holdId, borrowerId);

        return await cancelled.MatchAsync(
            async cancellation =>
            {
                if (cancellation.ReleasedCopyId is not null && queue.AnyoneIsWaiting)
                {
                    var today = clock.Today();

                    var blocked = await Standing.BlockedAmongAsync(
                            queue.QueuedBorrowersInOrder(), balances, policy, cancellationToken)
                        .ConfigureAwait(false);

                    queue.TrapOldestQueued(
                        cancellation.ReleasedCopyId,
                        policy.PickupDeadlineFor(today),
                        blocked);
                }

                return Result.Success();
            },
            errors => ValueTask.FromResult(Result.Failure(errors))).ConfigureAwait(false);
    }
}
