using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.ExpireUncollectedHolds;

/// <summary>
/// Handles <see cref="ExpireUncollectedHoldsCommand"/>.
/// </summary>
/// <param name="queues">The queues holding a copy whose deadline has passed.</param>
/// <param name="balances">The port Circulation declared for Charges, consulted per queued
/// borrower so a blocked one is skipped when the released copy is offered on.</param>
/// <param name="policy">The circulation policy — the pickup period the next borrower gets.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// The same standing question the return moment asks, for the same reason: a copy set aside for
/// someone who cannot collect it is precisely the waste the rule exists to prevent.
/// </remarks>
public sealed class ExpireUncollectedHoldsCommandHandler(
    IHoldQueueRepository queues,
    IMemberBalance balances,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<ExpireUncollectedHoldsCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        ExpireUncollectedHoldsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var today = clock.Today();

        var uncollected = await queues
            .WithHoldsAwaitingPickupThroughAsync(today.AddDays(-1), cancellationToken)
            .ConfigureAwait(false);

        foreach (var queue in uncollected)
        {
            var blocked = await Standing.BlockedAmongAsync(
                    queue.QueuedBorrowersInOrder(), balances, policy, cancellationToken)
                .ConfigureAwait(false);

            queue.ExpireUncollectedHolds(today, blocked, policy);
        }

        return Result.Success();
    }
}
