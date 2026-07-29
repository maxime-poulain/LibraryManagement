using System.Text.Json;
using System.Text.Json.Serialization;
using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// Serializes every strongly typed identifier as its underlying <see cref="Guid"/>, and rebuilds it
/// through the domain's own factory.
/// </summary>
/// <remarks>
/// One factory for all of them, present and future: a module never writes a converter for an
/// identifier. Reading goes through <c>FromValue</c>, the <see langword="static"/>
/// <see langword="abstract"/> member of <see cref="IEntityId{TSelf}"/> — reachable from a generic
/// without reflection per call, the same technique <c>IFailable</c> uses for its factory. The empty
/// <see cref="Guid"/> is refused by the identifier's own constructor: a payload carrying one is
/// corruption, and it throws rather than materializing an identifier that is not one.
/// </remarks>
public sealed class EntityIdJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert)
        => !typeToConvert.IsAbstract
           && typeToConvert.BaseType is { IsGenericType: true } baseType
           && baseType.GetGenericTypeDefinition() == typeof(EntityId<>)
           && baseType.GetGenericArguments()[0] == typeToConvert;

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(
            typeof(EntityIdJsonConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class EntityIdJsonConverter<TEntityId> : JsonConverter<TEntityId>
        where TEntityId : EntityId<TEntityId>, IEntityId<TEntityId>
    {
        public override TEntityId Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
            => TEntityId.FromValue(reader.GetGuid());

        public override void Write(
            Utf8JsonWriter writer,
            TEntityId value,
            JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }
}
