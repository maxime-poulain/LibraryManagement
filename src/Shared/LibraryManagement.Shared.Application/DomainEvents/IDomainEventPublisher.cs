using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Shared.Application.DomainEvents;

/// <summary>
/// Delivers one <see cref="IDomainEvent"/> to whichever handlers are registered for it.
/// </summary>
/// <remarks>
/// <para>
/// The delivery arm of the outbox: the drain rebuilds an event from its stored row and hands it
/// here, and this port finds the handlers by the event's <em>runtime</em> type. What carries the
/// delivery — the mediator today, a bus some day — is one implementation and one registration away,
/// which is the reason the drain depends on this and never on a mediator.
/// </para>
/// <para>
/// It takes one event, not a batch. The drain processes one message per transaction so that a
/// failure is attributable to exactly one row; a batch signature would invite delivery semantics —
/// all-or-nothing, best-effort — that the outbox has already settled at the row level.
/// </para>
/// </remarks>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Asynchronously delivers <paramref name="domainEvent"/> to its handlers.
    /// </summary>
    /// <param name="domainEvent">The event to deliver.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to signal cancellation of the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
