using LibraryManagement.Charges.PublishedLanguage;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Application.IntegrationEvents;
using DomainBalanceChanged = LibraryManagement.Charges.Domain.Accounts.MemberBalanceChanged;

namespace LibraryManagement.Charges.Infrastructure.IntegrationEvents;

/// <summary>
/// Flattens a movement of a member's balance into the fact other modules are allowed to see.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
/// <remarks>
/// <para>
/// Both amounts cross and no opinion does. This context states what its own arithmetic knows;
/// whether anything was crossed is decided on arrival, by the context that owns the threshold.
/// </para>
/// <para>
/// The domain event and the published contract share a name and are two types, which is the point
/// rather than an accident: one carries <c>Money</c> and lives where the arithmetic is, the other
/// carries <c>decimal</c> and lives in a project that references nothing.
/// </para>
/// </remarks>
public sealed class MemberBalanceChangedTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<DomainBalanceChanged>
{
    /// <inheritdoc/>
    public async ValueTask Handle(
        DomainBalanceChanged notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new MemberBalanceChanged(
                notification.EventId,
                notification.MemberId.Value,
                notification.PreviousBalance.Amount,
                notification.CurrentBalance.Amount),
            cancellationToken).ConfigureAwait(false);
    }
}
