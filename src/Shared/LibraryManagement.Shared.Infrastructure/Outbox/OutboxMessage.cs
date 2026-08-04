namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// One domain event, written down instead of executed: the row the outbox pattern revolves around.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately anemic — settable properties, no invariants, no behavior. This is not a domain
/// object that fell short; it is infrastructure's own bookkeeping, the persisted form of "this
/// still has to be delivered", and the processor is the only thing that reads or writes it.
/// </para>
/// <para>
/// Every module's store maps this type into its own schema, because the row must be written by the
/// same <c>SaveChangesAsync</c> as the change that raised the event — same context, same implicit
/// transaction, so the store can never hold the fact without the announcement nor the announcement
/// without the fact. A central outbox table would be a second context and a second transaction,
/// which is the exact failure the pattern exists to remove.
/// </para>
/// </remarks>
public sealed class OutboxMessage
{
    /// <summary>
    /// The drain order. An identity column rather than the event's own identifier, because a
    /// version 7 UUID only orders to the millisecond and the queue needs strict insertion order.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The event's own identifier, unique in the table. It is also what lets a handler recognize a
    /// redelivery: the outbox promises at-least-once, never exactly-once.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// The event's CLR type, as <c>"{FullName}, {AssemblySimpleName}"</c> — no version, no culture,
    /// so an assembly version bump does not orphan every stored row.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The event, serialized as JSON.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>When the event was raised, copied from the event itself.</summary>
    public DateTimeOffset OccurredOn { get; set; }

    /// <summary>When the row was written. The gap to <see cref="ProcessedOn"/> is the drain lag.</summary>
    public DateTimeOffset StoredOn { get; set; }

    /// <summary>When the event was delivered, or <see langword="null"/> while it waits.</summary>
    public DateTimeOffset? ProcessedOn { get; set; }

    /// <summary>How many deliveries have been attempted and failed.</summary>
    public int Attempts { get; set; }

    /// <summary>The last failure, in full, or <see langword="null"/> when none occurred.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// When the message was given up on, after <see cref="OutboxProcessor{TContext}.MaxAttempts"/>
    /// failures. A dead message is skipped so the queue behind it can move again; it stays in the
    /// table because it is an operator's problem now, and a problem that vanished is not solved.
    /// </summary>
    public DateTimeOffset? DeadOn { get; set; }
}
