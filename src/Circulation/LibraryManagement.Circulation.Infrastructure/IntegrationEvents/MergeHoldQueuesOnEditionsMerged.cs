using LibraryManagement.Catalog.PublishedLanguage;
using LibraryManagement.Circulation.Application.Holds.MergeHoldQueues;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

/// <summary>
/// Two catalog records became one, so the two queues waiting on them become one.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <remarks>
/// <para>
/// <strong>The expensive half of what a merge costs this context.</strong> Its sibling
/// <see cref="RepointLoansOnEditionsMerged"/> moves a field on every live loan; here the merged
/// identifier is the aggregate's own key, so nothing can be repointed — two queues have to become
/// one, against an invariant that had to be restated before they could.
/// </para>
/// <para>
/// <strong>A subscriber of its own, and not a second dispatch inside the first.</strong> The loans
/// and the queues are separate units of consistency, and the publisher runs every subscriber
/// registered for a contract. Splitting them keeps each command's transaction its own; the cost is
/// that a throw here replays both, which the loan sweep survives because it is idempotent — and so
/// is this one, since a replay finds the absorbed queue already empty.
/// </para>
/// <para>
/// <strong>It dispatches rather than writes</strong>, for the reason every subscriber here gives:
/// this runs inside Catalog's drain, whose save is on Catalog's context, so queues changed here
/// would be tracked where nobody saves.
/// </para>
/// </remarks>
public sealed class MergeHoldQueuesOnEditionsMerged(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<EditionsMerged>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        EditionsMerged contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(
                new MergeHoldQueuesCommand(
                    contract.AbsorbedEditionId,
                    contract.SurvivingEditionId),
                cancellationToken)
            .ConfigureAwait(false);

        var refusal = result.Match<string?>(
            () => null,
            errors => string.Join(" ", errors.Select(error => error.ToString())));

        if (refusal is not null)
        {
            throw new InvalidOperationException(
                $"Circulation refused to merge the hold queue of edition "
                + $"{contract.AbsorbedEditionId} into {contract.SurvivingEditionId}, which Catalog "
                + $"merged in event {contract.EventId}: {refusal}");
        }
    }
}
