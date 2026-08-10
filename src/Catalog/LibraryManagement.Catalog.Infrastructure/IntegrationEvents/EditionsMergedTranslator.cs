using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Application.IntegrationEvents;
using Contracts = LibraryManagement.Catalog.PublishedLanguage;

namespace LibraryManagement.Catalog.Infrastructure.IntegrationEvents;

/// <summary>
/// Flattens the merge into the fact the modules downstream of this one are allowed to see.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
/// <remarks>
/// <para>
/// The first translator in this module, and the first thing Catalog says on its own initiative.
/// Everything it published before was an answer to a question somebody asked it.
/// </para>
/// <para>
/// The translation happens here, on the announcing side, because it can happen nowhere else: a
/// consuming module may not reference <c>Catalog.Domain</c>, so it could not name
/// <see cref="EditionsMerged"/> to subscribe to it.
/// </para>
/// <para>
/// The domain event and the contract carry the same two identifiers, so this flattening looks like
/// a formality — and it is not one. The domain event carries <c>EditionId</c>, a type that belongs
/// to this context and is free to change; the contract carries <c>Guid</c>, which is not. The day
/// an identifier grows a part, this class is where that stops.
/// </para>
/// </remarks>
public sealed class EditionsMergedTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<EditionsMerged>
{
    /// <inheritdoc/>
    public async ValueTask Handle(EditionsMerged notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new Contracts.EditionsMerged(
                notification.EventId,
                notification.AbsorbedEditionId.Value,
                notification.SurvivingEditionId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}
