using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Shared.Application.DomainEvents;

/// <summary>
/// Represents an domain event publisher that is responsible for
/// dispatching and publishing <see cref="IDomainEvent"/>
/// of entities implementing the <see cref="IHasDomainEvents"/> interface.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Asynchronously publishes the <see cref="IDomainEvent"/> raised by the given objects.
    /// </summary>
    /// <param name="havingDomainEvents">The objects whose raised <see cref="IDomainEvent"/> are to be published.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to signal cancellation of the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask PublishAsync(
        IEnumerable<IHasDomainEvents> havingDomainEvents,
        CancellationToken cancellationToken);
}
