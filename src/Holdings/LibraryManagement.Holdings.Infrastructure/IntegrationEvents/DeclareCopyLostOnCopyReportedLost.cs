using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.Application.Copies.DeclareCopyLost;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Holdings.Infrastructure.IntegrationEvents;

/// <summary>
/// A copy Circulation stopped waiting for is a copy this module records as lost.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <remarks>
/// <para>
/// <strong>It dispatches rather than writes.</strong> The subscriber runs inside Circulation's
/// drain, whose save is on Circulation's context; changing a <c>Copy</c> here would leave it tracked
/// in a context nobody saves — persisted nowhere, reported by nothing. Dispatching puts the change
/// in Holdings' own transaction, through Holdings' own validator and handler, which is also what
/// keeps the decision in this module: Circulation announced an observation, and what an
/// unaccounted-for copy becomes stays Holdings' rule.
/// </para>
/// <para>
/// <strong>Redelivery is safe without a deduplication table.</strong> The consumer's command and the
/// announcing module's processed mark commit in two transactions, so a crash between them replays a
/// fact already acted on — and <c>Copy.DeclareLost</c> answers success for a copy already lost, a
/// choice the aggregate made for its own reasons long before this route existed. The contract's
/// event identity is there for the subscribers that will not be so lucky.
/// </para>
/// <para>
/// <strong>A refusal throws.</strong> Declaring a withdrawn copy lost fails, and a failure that
/// returned quietly would let Circulation mark the announcement delivered while nothing happened.
/// Throwing leaves the row unmarked, so the drain replays it and, if it keeps failing, dead-letters
/// it where an operator will see it — which is the correct end for a genuine contradiction between
/// two modules' records.
/// </para>
/// </remarks>
public sealed class DeclareCopyLostOnCopyReportedLost(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<CopyReportedLost>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        CopyReportedLost contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(new DeclareCopyLostCommand(contract.CopyId), cancellationToken)
            .ConfigureAwait(false);

        var refusal = result.Match<string?>(
            () => null,
            errors => string.Join(" ", errors.Select(error => error.ToString())));

        if (refusal is not null)
        {
            throw new InvalidOperationException(
                $"Holdings refused to record copy {contract.CopyId} as lost, which Circulation "
                + $"reported in event {contract.EventId}: {refusal}");
        }
    }
}
