using System.Text;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Editions;

/// <summary>
/// An International Standard Book Number — the identifier a published edition bears.
/// </summary>
/// <remarks>
/// <para>
/// Both forms are accepted, because both exist on title pages: ten digits before 2007, thirteen
/// since. Each is kept as given rather than rewritten into the other — an ISBN-10 is what the item
/// actually bears, and folding it into its 978 form would record something the title page does not
/// say. The two forms of one number are therefore two values, and two access points the day both
/// are recorded.
/// </para>
/// <para>
/// The check digit is verified, not stored on faith: it exists precisely to catch the transcription
/// errors a cataloguing desk produces, and an ISBN that fails its own checksum is a typo by
/// definition. Hyphens and spaces are grouping, not identity, so <c>978-2-07-061275-8</c> and
/// <c>9782070612758</c> are one value.
/// </para>
/// </remarks>
public sealed class Isbn : ValueObject<Isbn>
{
    /// <summary>The number of digits a normalized ISBN-13 carries — also the storage bound.</summary>
    public const int MaxLength = 13;

    /// <summary>The longest written form accepted: thirteen digits and four hyphens.</summary>
    public const int LongestWrittenForm = 17;

    private Isbn(string value) => Value = value;

    /// <summary>Gets the normalized number: digits only, with a final <c>X</c> where ISBN-10 uses one.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates an ISBN from its written form.
    /// </summary>
    /// <param name="value">The number as written, hyphens and spaces welcome.</param>
    /// <returns>The ISBN, or the reason the value is not one.</returns>
    public static Result<Isbn> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Isbn>.Failure(CatalogErrorCodes.InvalidIsbn, "An ISBN is required.");
        }

        var written = value.Trim();
        var normalized = new StringBuilder(MaxLength);

        foreach (var character in written)
        {
            if (char.IsAsciiDigit(character))
            {
                normalized.Append(character);
            }
            else if (character is 'x' or 'X')
            {
                normalized.Append('X');
            }
            else if (character is not ('-' or ' '))
            {
                return NotAnIsbn(written);
            }

            if (normalized.Length > MaxLength)
            {
                return NotAnIsbn(written);
            }
        }

        return normalized.Length switch
        {
            10 => CreateTen(written, normalized.ToString()),
            13 => CreateThirteen(written, normalized.ToString()),
            _ => NotAnIsbn(written),
        };
    }

    private static Result<Isbn> CreateTen(string written, string normalized)
    {
        // X stands for ten, and only the check digit may carry it.
        if (normalized.AsSpan(0, 9).Contains('X'))
        {
            return NotAnIsbn(written);
        }

        var sum = 0;

        for (var i = 0; i < 10; i++)
        {
            var digit = normalized[i] == 'X' ? 10 : normalized[i] - '0';
            sum += (10 - i) * digit;
        }

        return sum % 11 == 0
            ? Result<Isbn>.Success(new Isbn(normalized))
            : WrongCheckDigit(written);
    }

    private static Result<Isbn> CreateThirteen(string written, string normalized)
    {
        if (normalized.Contains('X', StringComparison.Ordinal))
        {
            return NotAnIsbn(written);
        }

        // Thirteen digits that do not begin with the book prefixes are an article number of some
        // other kind, not an ISBN wearing one more disguise.
        if (!normalized.StartsWith("978", StringComparison.Ordinal)
            && !normalized.StartsWith("979", StringComparison.Ordinal))
        {
            return Result<Isbn>.Failure(
                CatalogErrorCodes.InvalidIsbn,
                $"'{written}' is not an ISBN: a thirteen-digit ISBN begins with 978 or 979.");
        }

        var sum = 0;

        for (var i = 0; i < 13; i++)
        {
            sum += (i % 2 == 0 ? 1 : 3) * (normalized[i] - '0');
        }

        return sum % 10 == 0
            ? Result<Isbn>.Success(new Isbn(normalized))
            : WrongCheckDigit(written);
    }

    private static Result<Isbn> NotAnIsbn(string written)
        => Result<Isbn>.Failure(
            CatalogErrorCodes.InvalidIsbn,
            $"'{written}' is not an ISBN: ten or thirteen digits, with hyphens or spaces between groups.");

    private static Result<Isbn> WrongCheckDigit(string written)
        => Result<Isbn>.Failure(
            CatalogErrorCodes.InvalidIsbn,
            $"'{written}' is not an ISBN: the check digit does not match, so a digit was mistyped.");

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Returns the normalized number.</summary>
    public override string ToString() => Value;
}
