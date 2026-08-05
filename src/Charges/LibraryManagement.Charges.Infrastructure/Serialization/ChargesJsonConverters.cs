using System.Text.Json;
using System.Text.Json.Serialization;
using LibraryManagement.Charges.Domain.Accounts;

namespace LibraryManagement.Charges.Infrastructure.Serialization;

// The JSON side of what the store configuration already does: the value object travels as its
// underlying value, and the domain never learns that a serializer exists.
//
// Identifiers need no converter here — the shared EntityIdJsonConverterFactory covers every
// EntityId<T> there is or will be.

/// <summary>
/// Serializes <see cref="Money"/> as the amount itself.
/// </summary>
/// <remarks>
/// A number and not a string, because it is one: the outbox payload is read by machines and by
/// whoever is debugging a failed delivery, and an amount quoted like a name would invite somebody to
/// parse it back. Reading goes through <see cref="Money.Of"/>, so a negative amount in a stored row
/// is corruption and throws rather than materializing a value that cannot exist.
/// </remarks>
public sealed class MoneyJsonConverter : JsonConverter<Money>
{
    /// <inheritdoc/>
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var amount = reader.GetDecimal();

        try
        {
            return Money.Of(amount);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException(
                $"The outbox holds '{amount}' as an amount of money, which is not a valid one.",
                exception);
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteNumberValue(value.Amount);
    }
}
