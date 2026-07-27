using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Domain;
using Mediator;

namespace LibraryManagement.Shared.Infrastructure.DomainEvents;

/// <summary>
/// Publishes what the aggregates raised, through the mediator the rest of the pipeline already uses.
/// </summary>
/// <param name="publisher">Delivers a notification to whichever handlers registered for it.</param>
/// <remarks>
/// <para>
/// The in-process implementation, and the only place in the solution that names a mediator to publish
/// with. Everything upstream — the interceptor above all — depends on
/// <see cref="IDomainEventPublisher"/> and never learns what carries an event: which is what will let
/// the outbox implementation take this one's place by changing a single registration.
/// </para>
/// <para>
/// Events are taken and cleared <em>before</em> any of them is published. That ordering is the whole
/// of the contract: a handler is entitled to act on the same aggregate and raise a further event, and
/// clearing first is what leaves that new event in place to be found by the next round rather than
/// wiped by the end of this one.
/// </para>
/// <para>
/// A handler that throws is not caught here. Errors that a caller can act on travel as a
/// <c>Result</c>, and nothing about "the work happened, and someone listening broke" is such an
/// error: it is a defect, it should be loud, and it should stop the save that had not yet run.
/// </para>
/// </remarks>
public sealed class MediatorDomainEventPublisher(IPublisher publisher) : IDomainEventPublisher
{
    /// <inheritdoc/>
    public async ValueTask PublishAsync(
        IEnumerable<IHasDomainEvents> havingDomainEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(havingDomainEvents);

        var raised = new List<IDomainEvent>();

        foreach (var source in havingDomainEvents)
        {
            raised.AddRange(source.DomainEvents);
            source.ClearDomainEvents();
        }

        foreach (var domainEvent in raised)
        {
            // The object overload, deliberately. The generic one would close on IDomainEvent — the
            // static type of the variable — and go looking for handlers of the interface instead of
            // handlers of the event that was actually raised, finding none and reporting success.
            await publisher.Publish((object)domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
