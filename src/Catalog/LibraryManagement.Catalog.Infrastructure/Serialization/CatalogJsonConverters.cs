using System.Text.Json;
using System.Text.Json.Serialization;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Editions;
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
/// Serializes <see cref="NameForm"/> as the name itself.
/// </summary>
public sealed class NameFormJsonConverter : JsonConverter<NameForm>
{
    /// <inheritdoc/>
    public override NameForm Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return NameForm.Create(value).Match(
            name => name,
            _ => throw new InvalidOperationException(
                $"The outbox holds '{value}' as a name, which is not a valid one."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, NameForm value, JsonSerializerOptions options)
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

/// <summary>
/// Serializes <see cref="Isbn"/> as the normalized number itself.
/// </summary>
public sealed class IsbnJsonConverter : JsonConverter<Isbn>
{
    /// <inheritdoc/>
    public override Isbn Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return Isbn.Create(value).Match(
            isbn => isbn,
            _ => throw new InvalidOperationException(
                $"The outbox holds '{value}' as an ISBN, which is not a valid one."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Isbn value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Serializes <see cref="LifeYears"/> as an object holding both years.
/// </summary>
/// <remarks>
/// The one converter here that is not a single string: the value holds two facts, either of which
/// may be unknown, and flattening them into one string would invent a format only this converter
/// could read. Both properties are always written — <c>{"Birth":null,"Death":null}</c> is
/// <see cref="LifeYears.Unknown"/>, spelled out rather than special-cased.
/// </remarks>
public sealed class LifeYearsJsonConverter : JsonConverter<LifeYears>
{
    private const string BirthProperty = "Birth";
    private const string DeathProperty = "Death";

    /// <inheritdoc/>
    public override LifeYears Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException(
                "The outbox holds life years that are not the expected object of two years.");
        }

        int? birth = null;
        int? death = null;

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var property = reader.GetString();
            reader.Read();

            var year = reader.TokenType == JsonTokenType.Null ? (int?)null : reader.GetInt32();

            switch (property)
            {
                case BirthProperty:
                    birth = year;
                    break;
                case DeathProperty:
                    death = year;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"The outbox holds '{property}' among life years, which is not a year.");
            }
        }

        return LifeYears.Create(birth, death).Match(
            lifeYears => lifeYears,
            _ => throw new InvalidOperationException(
                $"The outbox holds '{birth}-{death}' as life years, which are not valid ones."));
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LifeYears value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        WriteYear(writer, BirthProperty, value.Birth);
        WriteYear(writer, DeathProperty, value.Death);
        writer.WriteEndObject();
    }

    private static void WriteYear(Utf8JsonWriter writer, string property, int? year)
    {
        if (year is null)
        {
            writer.WriteNull(property);
        }
        else
        {
            writer.WriteNumber(property, year.Value);
        }
    }
}
