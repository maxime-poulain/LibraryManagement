using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Holdings.Infrastructure.IntegrationEvents;

// The first facts this module announces beyond itself. Four departures from service flatten into
// one contract that names no status — what a departure means is each reader's own rule — and the
// find flattens into its own, because coming back is not a fifth kind of leaving. Every
// translator carries the event's identity across: delivery beyond this module is at-least-once,
// and a subscriber has nothing else to recognise a redelivery by.

/// <summary>
/// Announces a copy sent for repair as a departure from service.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
public sealed class CopySentForRepairTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<CopySentForRepair>
{
    /// <inheritdoc/>
    public async ValueTask Handle(CopySentForRepair notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new CopyLeftService(notification.EventId, notification.CopyId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Announces a copy restricted to consultation as a departure from service.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
public sealed class CopyRestrictedToReferenceTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<CopyRestrictedToReference>
{
    /// <inheritdoc/>
    public async ValueTask Handle(
        CopyRestrictedToReference notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new CopyLeftService(notification.EventId, notification.CopyId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Announces a withdrawal as a departure from service.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
public sealed class CopyWithdrawnTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<CopyWithdrawn>
{
    /// <inheritdoc/>
    public async ValueTask Handle(CopyWithdrawn notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new CopyLeftService(notification.EventId, notification.CopyId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Announces a declared loss as a departure from service.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
/// <remarks>
/// Announced whichever road declared it — this module's own moment, or the reaction to
/// Circulation's report. In the second case Circulation is told something it already knows and
/// its subscriber finds nothing set aside for the copy, which is a redelivery's ordinary shape,
/// not a reason to make the translator remember where the loss came from.
/// </remarks>
public sealed class CopyDeclaredLostTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<CopyDeclaredLost>
{
    /// <inheritdoc/>
    public async ValueTask Handle(CopyDeclaredLost notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new CopyLeftService(notification.EventId, notification.CopyId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Announces a copy that turned up, so what its absence cost can be settled.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
public sealed class CopyFoundTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<CopyFound>
{
    /// <inheritdoc/>
    public async ValueTask Handle(CopyFound notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new CopyRecovered(notification.EventId, notification.CopyId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}
