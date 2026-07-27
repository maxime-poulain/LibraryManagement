using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Authors;

/// <summary>
/// The years a person was born and died, either of which a catalogue may not know.
/// </summary>
/// <remarks>
/// A value object rather than two fields on <see cref="Author"/>, because the two are only
/// meaningful together: neither is wrong on its own, and the one rule that governs them — a person
/// does not die before being born — has nowhere else to live.
/// </remarks>
public sealed class LifeYears : ValueObject<LifeYears>
{
    /// <summary>The earliest year a catalogue will accept.</summary>
    public const int EarliestYear = 1;

    /// <summary>The latest year a catalogue will accept.</summary>
    /// <remarks>
    /// A fixed bound rather than "not in the future". The domain has no clock, and giving it one to
    /// reject a year typed two ahead would buy very little: an implausible year is a question about
    /// the field, and the command validator answers those.
    /// </remarks>
    public const int LatestYear = 2200;

    /// <summary>Neither year is known.</summary>
    public static readonly LifeYears Unknown = new(null, null);

    private LifeYears(int? birth, int? death)
    {
        Birth = birth;
        Death = death;
    }

    /// <summary>Gets the year of birth, or <see langword="null"/> when unknown.</summary>
    public int? Birth { get; }

    /// <summary>Gets the year of death, or <see langword="null"/> when unknown or still living.</summary>
    public int? Death { get; }

    /// <summary>
    /// Creates the pair, reporting every problem at once.
    /// </summary>
    /// <param name="birth">The year of birth, if known.</param>
    /// <param name="death">The year of death, if known.</param>
    /// <returns>The pair, or every reason it is not one.</returns>
    public static Result<LifeYears> Create(int? birth, int? death)
    {
        var errors = new ErrorCollection();

        if (birth is { } b && b is < EarliestYear or > LatestYear)
        {
            errors.Add(CatalogErrorCodes.InvalidLifeYears, OutOfRange("birth", b));
        }

        if (death is { } d && d is < EarliestYear or > LatestYear)
        {
            errors.Add(CatalogErrorCodes.InvalidLifeYears, OutOfRange("death", d));
        }

        if (birth is { } from && death is { } to && to < from)
        {
            errors.Add(
                CatalogErrorCodes.InvalidLifeYears,
                $"A year of death ({to}) cannot precede a year of birth ({from}).");
        }

        return errors.Count > 0
            ? Result<LifeYears>.Failure(errors)
            : Result<LifeYears>.Success(new LifeYears(birth, death));
    }

    private static string OutOfRange(string which, int year)
        => $"A year of {which} ({year}) must lie between {EarliestYear} and {LatestYear}.";

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Birth;
        yield return Death;
    }

    /// <summary>Returns the years in the form a catalogue prints them, such as <c>"1900-1944"</c>.</summary>
    public override string ToString() => (Birth, Death) switch
    {
        (null, null) => string.Empty,
        ({ } birth, null) => $"{birth}-",
        (null, { } death) => $"-{death}",
        var (birth, death) => $"{birth}-{death}",
    };
}
