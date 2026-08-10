using LibraryManagement.Catalog.PublishedLanguage;
using LibraryManagement.Holdings.Application.Copies.RepointCopiesOfMergedEdition;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Holdings.Infrastructure.IntegrationEvents;

/// <summary>
/// Two catalog records became one, so the copies of the absorbed record are filed under the survivor.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <remarks>
/// <para>
/// <strong>The first thing this module hears from Catalog.</strong> Every other exchange with that
/// context is a question Holdings asks — does this edition exist? — answered synchronously through
/// its published language. This is the one fact Catalog states on its own initiative, and it exists
/// because no constraint crosses a schema: nothing but an event can report that an identifier this
/// module holds in a column has stopped naming a record.
/// </para>
/// <para>
/// <strong>It dispatches rather than writes</strong>, for the reason
/// <see cref="DeclareCopyLostOnCopyReportedLost"/> gives at length: the subscriber runs inside
/// Catalog's drain, whose save is on Catalog's context, so a <c>Copy</c> changed here would be
/// tracked where nobody saves — persisted nowhere and reported by nothing. Dispatching puts the
/// change in Holdings' own transaction, and keeps what a merge costs this context a rule of this
/// context.
/// </para>
/// <para>
/// <strong>A refusal throws</strong>, so the announcing row stays unmarked, the drain replays it and
/// a fact that keeps failing ends where an operator will see it. Redelivery is safe: the command
/// sweeps by the absorbed identifier, which after the first delivery is on no copy at all.
/// </para>
/// </remarks>
public sealed class RepointCopiesOnEditionsMerged(ICommandDispatcher commands)
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
                new RepointCopiesOfMergedEditionCommand(
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
                $"Holdings refused to repoint the copies of edition {contract.AbsorbedEditionId} at "
                + $"{contract.SurvivingEditionId}, which Catalog merged in event {contract.EventId}: "
                + refusal);
        }
    }
}
