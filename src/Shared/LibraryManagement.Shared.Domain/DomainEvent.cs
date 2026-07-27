namespace LibraryManagement.Shared.Domain;

/// <summary>
/// The base for every domain event. Supplies the metadata required by <see cref="IDomainEvent"/>
/// so that concrete events only declare what actually happened.
/// </summary>
/// <example>
/// <code>
/// public sealed record CopyReturned(LoanId LoanId, CopyId CopyId, DateOnly ReturnedOn) : DomainEvent;
/// </code>
/// </example>
/// <remarks>
/// <para>
/// A record rather than a class: an event is an immutable statement about the past, so value
/// equality and non-destructive mutation are the right semantics.
/// </para>
/// <para>
/// Both members are <see langword="init"/>-settable so a test can pin them —
/// <c>evt with { OccurredOn = knownInstant }</c> — without the kernel having to thread a clock
/// through every aggregate. Production code never sets them.
/// </para>
/// </remarks>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc/>
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <inheritdoc/>
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
