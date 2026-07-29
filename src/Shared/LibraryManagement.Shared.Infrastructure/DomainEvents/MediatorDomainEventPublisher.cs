using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Domain;
using Mediator;

namespace LibraryManagement.Shared.Infrastructure.DomainEvents;

/// <summary>
/// Delivers a domain event through the mediator the rest of the pipeline already uses.
/// </summary>
/// <param name="publisher">Delivers a notification to whichever handlers registered for it.</param>
/// <remarks>
/// <para>
/// The only place in the solution that names a mediator to deliver an event with. The outbox
/// processor depends on <see cref="IDomainEventPublisher"/> and never learns what carries a
/// delivery, so replacing this — with a bus, with anything — is one registration.
/// </para>
/// <para>
/// A handler that throws is not caught here. The processor above is what turns a failed delivery
/// into a recorded attempt and, eventually, a dead letter; swallowing the exception at this level
/// would report a delivery that never happened.
/// </para>
/// </remarks>
public sealed class MediatorDomainEventPublisher(IPublisher publisher) : IDomainEventPublisher
{
    /// <inheritdoc/>
    public async ValueTask PublishAsync(
        IDomainEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // The object overload, deliberately. The generic one would close on IDomainEvent — the
        // static type of the parameter — and go looking for handlers of the interface instead of
        // handlers of the event that was actually raised, finding none and reporting success.
        await publisher.Publish((object)domainEvent, cancellationToken).ConfigureAwait(false);
    }
}
