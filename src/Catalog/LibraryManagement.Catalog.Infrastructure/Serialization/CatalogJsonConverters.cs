using System.Text.Json;
using System.Text.Json.Serialization;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Infrastructure.Serialization;

// The JSON side of what the store configurations already do with HasConversion: each value object
// travels as its underlying value, and the domain never learns that a serializer exists. Reading
// goes back through Create, and a payload Create refuses is corruption — same philosophy as
// materialization: trust the store, throw on a row that lies, repair by migration.
//
// Identifiers need no converter here: the shared EntityIdJsonConverterFactory covers every
// EntityId<T> there is or will be.

/// <summary>
/// Serializes <see cref="PersonName"/> as the heading itself.
/// </summary>
public sealed class PersonNameJsonConverter : JsonConverter<PersonName>
{
    /// <inheritdoc/>
    public override PersonName Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return PersonName.Create(value).Match(
            name => name,
            _ => throw new InvalidOperationException(
                $"The outbox holds '{value}' as a name, which is not a valid one."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, PersonName value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Serializes <see cref="Title"/> as the title itself.
/// </summary>
public sealed class TitleJsonConverter : JsonConverter<Title>
{
    /// <inheritdoc/>
    public override Title Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return Title.Create(value).Match(
            title => title,
            _ => throw new InvalidOperationException(
                $"The outbox holds '{value}' as a title, which is not a valid one."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Title value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.Value);
    }
}
