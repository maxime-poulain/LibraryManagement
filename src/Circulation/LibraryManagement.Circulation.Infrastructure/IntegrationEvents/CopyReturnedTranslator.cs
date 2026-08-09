using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Application.IntegrationEvents;
using Contracts = LibraryManagement.Circulation.PublishedLanguage;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

/// <summary>
/// Announces the physical fact of a return — the copy, and nothing else — for whoever keeps the
/// stock.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
/// <remarks>
/// The second translator on the returned loan, and a different audience than the first: the
/// lateness travels to whoever prices time, the copy to whoever must agree with the shelf. This
/// module's own tactical design long said a return changes nothing in Holdings, and the reversal
/// is argued where that sentence was — a copy Holdings holds as unaccounted for, back in hand
/// over the desk, stayed lost until a human happened to notice.
/// </remarks>
public sealed class CopyReturnedTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<LoanReturned>
{
    /// <inheritdoc/>
    public async ValueTask Handle(LoanReturned notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new Contracts.CopyReturned(notification.EventId, notification.CopyId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}
