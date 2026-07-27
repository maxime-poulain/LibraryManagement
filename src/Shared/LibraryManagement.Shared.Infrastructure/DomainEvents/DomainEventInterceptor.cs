using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LibraryManagement.Shared.Infrastructure.DomainEvents;

/// <summary>
/// Publishes the events the aggregates raised, immediately before what they changed is written.
/// </summary>
/// <param name="publisher">Delivers the events and clears them from the aggregates that raised them.</param>
/// <remarks>
/// <para>
/// <strong>Before the save, not after, and the outbox is why.</strong> Publishing will eventually
/// mean writing a row per event to an outbox table, which Hangfire drains and dispatches afterwards.
/// That row has to be written by the same <c>SaveChangesAsync</c> as the change that caused it —
/// one call, one implicit transaction — so that the store can never hold the fact without the
/// announcement, nor the announcement without the fact. Publish after the write and there are two
/// transactions, the second free to fail on its own, which is the failure the outbox pattern exists
/// to remove. That is the whole reason this intercepts <c>SavingChanges</c> and not
/// <c>SavedChanges</c>.
/// </para>
/// <para>
/// Publishing first has a second effect for as long as handlers still run in process: what a handler
/// changes joins the same save. That is worth having, but it is a consequence rather than the reason,
/// and the outbox will end it — an event drained by Hangfire is handled later, in a transaction of
/// its own, with nothing left to join.
/// </para>
/// <para>
/// The price of publishing first is that the set of events is not known in advance, since handlers
/// add to it as they run. So this drains rather than sweeps once: every round asks the change tracker
/// again, and rounds continue until nothing more has been raised. Two handlers each raising the
/// other's event would feed that loop forever, so it is bounded — an exception naming the problem is
/// worth more than a request that never comes back.
/// </para>
/// <para>
/// An interceptor rather than a pipeline behavior, although the plan had called for a behavior. A
/// behavior would have to <em>find</em> the aggregates that raised something, which means asking
/// whatever holds the change tracker, which means teaching the shared pipeline about the store it
/// was written not to know. The interceptor is already standing next to the answer.
/// </para>
/// </remarks>
public sealed class DomainEventInterceptor(IDomainEventPublisher publisher) : SaveChangesInterceptor
{
    private const int MaxRounds = 8;

    private bool _publishing;

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        // A handler that saves for itself re-enters this method on the same context. The flag keeps
        // the nested save from starting a second publication on top of the one still running: its
        // changes are already tracked, and the round in progress will find whatever they raised.
        if (eventData.Context is not null && !_publishing)
        {
            _publishing = true;

            try
            {
                await PublishUntilDrainedAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _publishing = false;
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="NotSupportedException">Always.</exception>
    /// <remarks>
    /// Publishing is asynchronous and there is no honest synchronous shape for it: blocking on the
    /// handlers would trade a deadlock for a convenience nothing here asks for. Doing nothing was the
    /// other option, and it is worse — a synchronous save would write the rows and drop every event
    /// on the floor without a word. Nothing in this solution saves synchronously.
    /// </remarks>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
        => throw new NotSupportedException(
            "This store saves asynchronously. A synchronous SaveChanges would write the rows and "
            + "publish none of the domain events they raised. Call SaveChangesAsync.");

    private async Task PublishUntilDrainedAsync(DbContext context, CancellationToken cancellationToken)
    {
        for (var round = 0; round < MaxRounds; round++)
        {
            // Materialised before publishing: handlers add and change entities, and the tracker
            // cannot be enumerated while that happens.
            var raising = context.ChangeTracker
                .Entries<IHasDomainEvents>()
                .Select(entry => entry.Entity)
                .Where(entity => entity.DomainEvents.Count > 0)
                .ToArray();

            if (raising.Length == 0)
            {
                return;
            }

            await publisher.PublishAsync(raising, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Domain events were still being raised after {MaxRounds} rounds of publication. Two "
            + "handlers are feeding each other: one raises an event whose handler raises the first "
            + "one again. Break the cycle in the handlers — nothing here can decide which of them "
            + "should stop.");
    }
}
