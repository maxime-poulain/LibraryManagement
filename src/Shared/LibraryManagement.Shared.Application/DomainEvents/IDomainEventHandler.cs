using LibraryManagement.Shared.Domain;
using Mediator;

namespace LibraryManagement.Shared.Application.DomainEvents;

/// <summary>
/// Represents a handler for a specific type of <see cref="IDomainEvent"/> within
/// a bounded context.
///
/// <para>
/// Each handler is responsible for executing specific logic when a Domain Event of a
/// corresponding type is raised. For example, an 'OrderReceivedHandler' could be an
/// <see cref="IDomainEventHandler{TDomainEvent}"/> for 'OrderReceived' Domain Events,
/// responsible for tasks such as updating the order status or logging the event.
/// </para>
///
/// <para>
/// In a microservice architecture, implementing Domain Event handlers provides a modular
/// way to manage actions in response to state changes. It ensures that logic is separated
/// and only triggered when necessary, aiding in maintainability and extensibility.
/// </para>
///
/// <para>
/// Domain Events themselves are declared in the domain layer, but reacting to them is an
/// application concern: handlers orchestrate use cases and reach out to infrastructure,
/// which is why this abstraction lives in the application layer.
/// </para>
/// </summary>
/// <typeparam name="TDomainEvent">The type of the domain event being handled.</typeparam>
public interface IDomainEventHandler<in TDomainEvent> : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
}
