using LibraryManagement.Catalog.PublishedLanguage;
using LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedEdition;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

/// <summary>
/// Two catalog records became one, so the loans still out on the absorbed record answer to the
/// survivor.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <remarks>
/// <para>
/// <strong>The first thing this context hears from Catalog, and the first edge between them.</strong>
/// Circulation learns an edition's identity from Holdings at checkout and has never asked Catalog
/// anything; this is the one fact Catalog states on its own initiative, and nothing but an event
/// could carry it, because no constraint crosses a schema.
/// </para>
/// <para>
/// <strong>It settles half of what a merge costs here.</strong> A loan holds the identifier in a
/// field and this moves it; a <c>HoldQueue</c> is an aggregate <em>keyed</em> by it, and making two
/// queues into one has rules of its own that are deliberately a separate change. Until that lands
/// the model is halfway converged — a returning copy consults the survivor's queue and not the
/// absorbed one's, where before it consulted the absorbed one and not the survivor's. Nothing
/// breaks; nothing is fully right either, and saying so is better than discovering it.
/// </para>
/// <para>
/// <strong>It dispatches rather than writes</strong>, for the reason every subscriber here gives:
/// this runs inside Catalog's drain, whose save is on Catalog's context, so a loan changed here
/// would be tracked where nobody saves — persisted nowhere and reported by nothing.
/// </para>
/// <para>
/// <strong>A refusal throws</strong>, leaving the announcing row unmarked for the next run and,
/// failing repeatedly, for an operator. Redelivery is safe: the command sweeps by the absorbed
/// identifier, which after the first delivery is on no live loan.
/// </para>
/// </remarks>
public sealed class RepointLoansOnEditionsMerged(ICommandDispatcher commands)
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
                new RepointLoansOfMergedEditionCommand(
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
                $"Circulation refused to repoint the loans of edition {contract.AbsorbedEditionId} "
                + $"at {contract.SurvivingEditionId}, which Catalog merged in event "
                + $"{contract.EventId}: {refusal}");
        }
    }
}
