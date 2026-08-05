namespace LibraryManagement.Shared.Application.IntegrationEvents;

/// <summary>
/// Reacts, on behalf of one module, to a fact another module announced.
/// </summary>
/// <typeparam name="TContract">
/// The announcing module's published contract — a record of primitives, constrained to nothing.
/// </typeparam>
/// <remarks>
/// <para>
/// <strong>There is no constraint on <typeparamref name="TContract"/>, and there cannot be.</strong>
/// A contract lives in a <c>*.PublishedLanguage</c> project, and those reference nothing at all — so
/// a contract cannot implement a marker from this assembly, nor the mediator's. The genericity is
/// therefore the entire mechanism: subscribers are resolved from the container by the contract's own
/// type, which is also why this hop does not travel through the mediator the way a domain event does.
/// </para>
/// <para>
/// <strong>A subscriber does not write.</strong> The drain saves the <em>announcing</em> module's
/// context, so a subscriber that changed its own module's aggregates would leave them in a context
/// nobody saves — tracked, never persisted, and no failure reported anywhere. It translates the
/// contract into a command of its own module and dispatches it instead, which puts the change in its
/// own transaction, decided by its own handler. See <c>docs/outbox.md</c> §9.
/// </para>
/// <para>
/// <strong>A subscriber owes idempotence.</strong> The consumer's command commits in one transaction
/// and the announcing module's processed mark in another, so a crash between them replays a fact
/// already acted on. Deduplicate by the event's identity, or reach for a domain operation that is
/// already idempotent.
/// </para>
/// <para>
/// Throwing is how a subscriber refuses. The exception travels up into the drain, which leaves the
/// row unmarked and replays it — so a failure to react is never mistaken for a reaction.
/// </para>
/// </remarks>
public interface IIntegrationEventSubscriber<in TContract>
{
    /// <summary>
    /// Asynchronously reacts to <paramref name="contract"/>.
    /// </summary>
    /// <param name="contract">The fact the other module announced.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to signal cancellation of the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask HandleAsync(TContract contract, CancellationToken cancellationToken = default);
}
