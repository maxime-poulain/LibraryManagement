using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

/// <summary>
/// Flattens the loan Circulation gave up on into the fact other modules are allowed to see.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
/// <remarks>
/// <para>
/// The translation happens here, on the announcing side, because it can happen nowhere else: a
/// consuming module may not reference <c>Circulation.Domain</c>, so it could not name
/// <see cref="LoanDeclaredLost"/> to subscribe to it. What leaves this class is already flat.
/// </para>
/// <para>
/// It lives in the infrastructure rather than the application for the reason the projections do:
/// translating for the outside world is not a use case of this module, and there is no domain model
/// here to protect — the event is the contract, and it is the domain's own.
/// </para>
/// <para>
/// The event's identity is carried across deliberately. Delivery beyond this module is
/// at-least-once, and a subscriber that needs to recognise a redelivery has nothing else to do it
/// with; the same occurrence therefore keeps one identity on both sides of the boundary.
/// </para>
/// </remarks>
public sealed class LoanDeclaredLostTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<LoanDeclaredLost>
{
    /// <inheritdoc/>
    public async ValueTask Handle(LoanDeclaredLost notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new CopyReportedLost(notification.EventId, notification.CopyId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}
