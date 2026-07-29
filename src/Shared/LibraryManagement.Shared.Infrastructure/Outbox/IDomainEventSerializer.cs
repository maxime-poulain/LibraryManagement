using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// A domain event, in the form the outbox stores: its type name and its payload.
/// </summary>
/// <param name="Type">The event's CLR type, as <c>"{FullName}, {AssemblySimpleName}"</c>.</param>
/// <param name="Payload">The event, serialized as JSON.</param>
public readonly record struct SerializedDomainEvent(string Type, string Payload);

/// <summary>
/// Converts a domain event to and from the form the outbox stores.
/// </summary>
/// <remarks>
/// <para>
/// Domain events carry the domain's own types — identifiers with closed constructors, value objects
/// that validate on creation — and none of that is negotiable for a serializer's benefit, any more
/// than it was for Entity Framework's. The implementation owns converters instead, the exact mirror
/// of the value conversions in the store mapping: infrastructure translates at the border, and the
/// domain never learns that a border exists.
/// </para>
/// <para>
/// Renaming or moving an event type is a breaking change to every stored row that carries it: the
/// type name is the address deserialization dials. A rename must either wait for the table to drain
/// or ship with a migration that rewrites the stored names.
/// </para>
/// </remarks>
public interface IDomainEventSerializer
{
    /// <summary>
    /// Serializes <paramref name="domainEvent"/> for storage.
    /// </summary>
    /// <param name="domainEvent">The event to serialize.</param>
    /// <returns>The stored form.</returns>
    SerializedDomainEvent Serialize(IDomainEvent domainEvent);

    /// <summary>
    /// Rebuilds the event a row was stored from.
    /// </summary>
    /// <param name="type">The stored type name.</param>
    /// <param name="payload">The stored JSON.</param>
    /// <returns>The event, of its original runtime type.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type no longer exists or the payload no longer matches it. Deserialization
    /// trusts the store the way materialization does; a row it cannot read is corruption, and the
    /// repair belongs to a migration rather than to a silent skip.
    /// </exception>
    IDomainEvent Deserialize(string type, string payload);
}
