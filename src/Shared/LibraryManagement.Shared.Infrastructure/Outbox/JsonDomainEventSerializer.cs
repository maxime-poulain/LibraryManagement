using System.Text.Json;
using System.Text.Json.Serialization;
using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// Implements <see cref="IDomainEventSerializer"/> over System.Text.Json.
/// </summary>
/// <remarks>
/// A singleton with one options instance built once: converters are stateless, and building options
/// per call would defeat System.Text.Json's metadata caching. Events serialize as positional
/// records — the primary constructor rebuilds them — and <c>EventId</c> and <c>OccurredOn</c>
/// round-trip through their <c>init</c> setters, which matters: a redelivered event must carry the
/// identity it was raised with, or deduplication has nothing to hold on to.
/// </remarks>
public sealed class JsonDomainEventSerializer : IDomainEventSerializer
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonDomainEventSerializer"/> class.
    /// </summary>
    /// <param name="converters">
    /// The value-object converters the modules registered, each the mirror of a store conversion.
    /// The identifier converter is built in, since one factory covers every <c>EntityId</c> there
    /// will be.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="converters"/> is null.</exception>
    public JsonDomainEventSerializer(IEnumerable<JsonConverter> converters)
    {
        ArgumentNullException.ThrowIfNull(converters);

        _options = new JsonSerializerOptions();
        _options.Converters.Add(new EntityIdJsonConverterFactory());

        foreach (var converter in converters)
        {
            _options.Converters.Add(converter);
        }
    }

    /// <inheritdoc/>
    public SerializedDomainEvent Serialize(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var type = domainEvent.GetType();

        // Full name plus simple assembly name, never AssemblyQualifiedName: the qualified form pins
        // the version, and bumping an assembly would orphan every row written before the bump.
        return new SerializedDomainEvent(
            $"{type.FullName}, {type.Assembly.GetName().Name}",
            JsonSerializer.Serialize(domainEvent, type, _options));
    }

    /// <inheritdoc/>
    public IDomainEvent Deserialize(string type, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var eventType = Type.GetType(type) ?? throw new InvalidOperationException(
            $"The outbox holds an event of type '{type}', which no longer exists. Renaming or "
            + "moving an event type is a breaking change to the rows already stored under the old "
            + "name; the repair is a migration rewriting them, not a skip.");

        return (IDomainEvent?)JsonSerializer.Deserialize(payload, eventType, _options)
               ?? throw new InvalidOperationException(
                   $"The outbox payload for '{type}' deserialized to null.");
    }
}
