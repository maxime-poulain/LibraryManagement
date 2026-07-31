using System.Text.Json;
using System.Text.Json.Serialization;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Infrastructure.Serialization;

// The JSON side of what the store configuration already does with HasConversion: each value object
// travels as its underlying value, and the domain never learns that a serializer exists. Reading
// goes back through Create, and a payload Create refuses is corruption — trust the store, throw on a
// row that lies, repair by migration.
//
// Identifiers need no converter here: the shared EntityIdJsonConverterFactory covers every
// EntityId<T> there is or will be, this module's EditionId included.

/// <summary>
/// Serializes <see cref="Barcode"/> as the label itself.
/// </summary>
public sealed class BarcodeJsonConverter : JsonConverter<Barcode>
{
    /// <inheritdoc/>
    public override Barcode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return Barcode.Create(value).Match(
            barcode => barcode,
            _ => throw new InvalidOperationException(
                $"The outbox holds '{value}' as a barcode, which is not a valid one."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Barcode value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Serializes <see cref="Shelfmark"/> as the shelfmark itself.
/// </summary>
public sealed class ShelfmarkJsonConverter : JsonConverter<Shelfmark>
{
    /// <inheritdoc/>
    public override Shelfmark Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return Shelfmark.Create(value).Match(
            shelfmark => shelfmark,
            _ => throw new InvalidOperationException(
                $"The outbox holds '{value}' as a shelfmark, which is not a valid one."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Shelfmark value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.Value);
    }
}
