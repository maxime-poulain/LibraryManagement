using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.RemindOfHoldsExpiringSoon;

/// <summary>
/// Handles <see cref="RemindOfHoldsExpiringSoonCommand"/>.
/// </summary>
/// <param name="queues">The queues holding a copy whose deadline is within reach.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// The store narrows by deadline; each queue decides which of its own claims the day concerns —
/// claims already past their deadline are left for the expiry to end, not hurried along.
/// </remarks>
public sealed class RemindOfHoldsExpiringSoonCommandHandler(
    IHoldQueueRepository queues,
    TimeProvider clock) : ICommandHandler<RemindOfHoldsExpiringSoonCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RemindOfHoldsExpiringSoonCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var today = clock.Today();

        var expiring = await queues
            .WithHoldsAwaitingPickupThroughAsync(today.AddDays(1), cancellationToken)
            .ConfigureAwait(false);

        foreach (var queue in expiring)
        {
            queue.WarnOfImminentExpiry(today);
        }

        return Result.Success();
    }
}
