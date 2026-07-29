using LibraryManagement.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// Writes the events the aggregates raised into the outbox, inside the very save that persists what
/// they changed.
/// </summary>
/// <param name="serializer">Turns an event into the form the table stores.</param>
/// <param name="clock">Stamps when the row was written.</param>
/// <remarks>
/// <para>
/// <strong>Inside the save is the whole pattern.</strong> The row and the change that raised it go
/// through one <c>SaveChangesAsync</c>, therefore one implicit transaction: the store can never hold
/// the fact without the announcement, nor the announcement without the fact. Nothing is executed
/// here — handlers run later, when the processor drains the table, each in a transaction of its own.
/// That is also why no invariant may ride on an event handler: an event is handled <em>later</em>,
/// and an invariant that waits is not an invariant.
/// </para>
/// <para>
/// One sweep suffices where a bounded drain loop used to run. The loop existed because in-process
/// handlers could raise further events mid-publication; nothing runs during the save anymore, so
/// nothing can add to the set being swept. For the same reason the reentrancy guard is gone — no
/// handler exists in-save to re-enter through.
/// </para>
/// <para>
/// Both the synchronous and asynchronous paths are supported. The previous incarnation threw on the
/// synchronous one because publishing was asynchronous, and blocking on handlers trades a deadlock
/// for a convenience; serializing and adding a row is synchronous work, so the reason is gone and
/// the path is honest now.
/// </para>
/// <para>
/// Rows added here are picked up by the save in progress: an entity put into the change tracker
/// during <c>SavingChanges</c> enters in the Added state and is included — the previous design
/// depended on the same fact and proved it.
/// </para>
/// </remarks>
public sealed class OutboxInterceptor(IDomainEventSerializer serializer, TimeProvider clock)
    : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            WriteDown(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            WriteDown(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteDown(DbContext context)
    {
        // Materialised before writing: adding rows changes the tracker being enumerated.
        var raising = context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToArray();

        if (raising.Length == 0)
        {
            return;
        }

        var storedOn = clock.GetUtcNow();
        var outbox = context.Set<OutboxMessage>();

        foreach (var source in raising)
        {
            foreach (var domainEvent in source.DomainEvents)
            {
                var serialized = serializer.Serialize(domainEvent);

                outbox.Add(new OutboxMessage
                {
                    EventId = domainEvent.EventId,
                    Type = serialized.Type,
                    Payload = serialized.Payload,
                    OccurredOn = domainEvent.OccurredOn,
                    StoredOn = storedOn,
                });
            }

            // Cleared so an aggregate that stays tracked into a later save does not write its
            // history twice — the unique index on EventId would refuse it loudly, but a refusal
            // that never has a reason to fire is better still.
            source.ClearDomainEvents();
        }
    }
}
