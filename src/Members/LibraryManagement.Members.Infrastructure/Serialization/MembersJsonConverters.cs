using System.Text.Json;
using System.Text.Json.Serialization;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Infrastructure.Serialization;

// The JSON side of what the store configuration already does: each value object travels as its
// underlying values, and the domain never learns that a serializer exists. Reading goes back
// through Create, and a payload Create refuses is corruption — trust the store, throw on a row
// that lies, repair by migration.
//
// Two of these write objects rather than strings, a first for the outbox: a name and a set of
// channels are values with parts, and flattening either into a delimited string would invent a
// syntax someone would eventually have to parse back. The property names are the value objects'
// own, exactly as a record's positional parameters serialize — and they are contract, like every
// stored payload: renaming a part is the same decision as renaming an event parameter.
//
// Identifiers need no converter here: the shared EntityIdJsonConverterFactory covers every
// EntityId<T> there is or will be, MemberId included.

/// <summary>
/// Serializes <see cref="CardNumber"/> as the number itself.
/// </summary>
public sealed class CardNumberJsonConverter : JsonConverter<CardNumber>
{
    /// <inheritdoc/>
    public override CardNumber Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return CardNumber.Create(value).Match(
            cardNumber => cardNumber,
            _ => throw new InvalidOperationException(
                $"The outbox holds '{value}' as a card number, which is not a valid one."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, CardNumber value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Serializes <see cref="MemberName"/> as its two parts.
/// </summary>
public sealed class MemberNameJsonConverter : JsonConverter<MemberName>
{
    private sealed record Parts(string? GivenName, string? FamilyName);

    /// <inheritdoc/>
    public override MemberName Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var parts = JsonSerializer.Deserialize<Parts>(ref reader, options);

        return MemberName.Create(parts?.GivenName, parts?.FamilyName).Match(
            name => name,
            _ => throw new InvalidOperationException(
                "The outbox holds a member name that is not a valid one."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, MemberName value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteString(nameof(MemberName.GivenName), value.GivenName);
        writer.WriteString(nameof(MemberName.FamilyName), value.FamilyName);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Serializes <see cref="ContactDetails"/> as its channels, absent ones as nulls.
/// </summary>
public sealed class ContactDetailsJsonConverter : JsonConverter<ContactDetails>
{
    private sealed record Channels(string? Email, string? Phone, string? PostalAddress);

    /// <inheritdoc/>
    public override ContactDetails Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var channels = JsonSerializer.Deserialize<Channels>(ref reader, options);

        return ContactDetails.Create(channels?.Email, channels?.Phone, channels?.PostalAddress)
            .Match(
                contact => contact,
                _ => throw new InvalidOperationException(
                    "The outbox holds contact details that are not valid ones."));
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        ContactDetails value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteString(nameof(ContactDetails.Email), value.Email);
        writer.WriteString(nameof(ContactDetails.Phone), value.Phone);
        writer.WriteString(nameof(ContactDetails.PostalAddress), value.PostalAddress);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Serializes <see cref="Guardian"/> as a name and channels, each through its own converter.
/// </summary>
public sealed class GuardianJsonConverter : JsonConverter<Guardian>
{
    private sealed record Parts(MemberName? Name, ContactDetails? Contact);

    /// <inheritdoc/>
    public override Guardian Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        // The nested parts go back through their own converters, which is the point of composing
        // rather than flattening: one shape per value object, declared once.
        var parts = JsonSerializer.Deserialize<Parts>(ref reader, options);

        if (parts?.Name is null || parts.Contact is null)
        {
            throw new InvalidOperationException(
                "The outbox holds a guardian without a name or channels, which is not a valid one.");
        }

        return Guardian.Of(parts.Name, parts.Contact);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Guardian value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WritePropertyName(nameof(Guardian.Name));
        JsonSerializer.Serialize(writer, value.Name, options);
        writer.WritePropertyName(nameof(Guardian.Contact));
        JsonSerializer.Serialize(writer, value.Contact, options);
        writer.WriteEndObject();
    }
}
