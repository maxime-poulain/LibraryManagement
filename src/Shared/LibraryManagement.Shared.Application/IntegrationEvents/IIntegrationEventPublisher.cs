namespace LibraryManagement.Shared.Application.IntegrationEvents;

/// <summary>
/// Announces one module's fact to whichever other modules subscribed to it.
/// </summary>
/// <remarks>
/// <para>
/// Called from a translator on the <em>announcing</em> side: a domain event handler that receives
/// its own module's event and flattens it into the contract. It has to be that way round — a
/// consumer may not reference another module's <c>Domain</c>, so it could not name the event to
/// subscribe to it directly.
/// </para>
/// <para>
/// Delivery is synchronous within the drain's own delivery of the domain event, which is what makes
/// a subscriber's refusal block the announcing module's outbox row. The alternative — announcing
/// after the row is marked — would be the same two-transactions-in-the-wrong-order mistake the
/// outbox exists to remove, one level up.
/// </para>
/// </remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Asynchronously delivers <paramref name="contract"/> to its subscribers.
    /// </summary>
    /// <typeparam name="TContract">The published contract's type.</typeparam>
    /// <param name="contract">The fact to announce.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to signal cancellation of the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask PublishAsync<TContract>(
        TContract contract,
        CancellationToken cancellationToken = default)
        where TContract : notnull;
}
