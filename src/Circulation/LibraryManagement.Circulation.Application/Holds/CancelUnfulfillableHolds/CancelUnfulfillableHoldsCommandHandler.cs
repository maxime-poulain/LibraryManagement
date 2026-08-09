using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelUnfulfillableHolds;

/// <summary>
/// Handles <see cref="CancelUnfulfillableHoldsCommand"/>.
/// </summary>
/// <param name="queues">Every queue holding live claims.</param>
/// <param name="copies">Holdings' published language: can this edition still serve anyone?</param>
/// <remarks>
/// One question per live queue, answered from the shelves as they stand — a few dozen queues in a
/// municipal library, one indexed existence check each. Idempotent by state: a queue emptied
/// today is not returned tomorrow.
/// </remarks>
public sealed class CancelUnfulfillableHoldsCommandHandler(
    IHoldQueueRepository queues,
    ICopyLendability copies) : ICommandHandler<CancelUnfulfillableHoldsCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CancelUnfulfillableHoldsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var live = await queues.WithLiveHoldsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var queue in live)
        {
            var somethingToServe = await copies
                .AnyCopyExpectedToServeAsync(queue.Id.Value, cancellationToken)
                .ConfigureAwait(false);

            if (!somethingToServe)
            {
                queue.CancelAllUnfulfillable();
            }
        }

        return Result.Success();
    }
}
