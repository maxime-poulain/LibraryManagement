using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Domain.Copies;

/// <summary>
/// Where this copy stands on the shelves — <c>843.912 SAI</c>, or <c>JEUN 843.912 SAI</c> behind a
/// section prefix.
/// </summary>
/// <remarks>
/// <para>
/// Per copy, never per edition: one copy of a title lives in the children's section and another in
/// the reserve, and that is the ordinary case.
/// </para>
/// <para>
/// <strong>Composed from Catalog's facts, owned here, and derived once.</strong> A library builds it
/// from the class number and the first letters of the author's preferred name — and then prints it
/// on a label and sticks it to a spine. It is not a computed view of Catalog data, which is why this
/// is a plain string the module is handed rather than something assembled from parts: when a
/// cataloger later corrects that preferred name, the labels on the shelves do not move, and neither
/// does this. Relabelling a shelf after a reclassification is work a library schedules, and it goes
/// through a decision.
/// </para>
/// <para>
/// The section is a prefix today rather than a field of its own. It becomes a field the day someone
/// wants to count a section, and that is a report nobody has asked for.
/// </para>
/// </remarks>
public sealed class Shelfmark : ValueObject<Shelfmark>
{
    /// <summary>The greatest number of characters a shelfmark may run to.</summary>
    /// <remarks>Long enough for a section prefix, a class number and a cutter.</remarks>
    public const int MaxLength = 64;

    private Shelfmark(string value) => Value = value;

    /// <summary>Gets the shelfmark.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a shelfmark, trimming the surrounding white space.
    /// </summary>
    /// <param name="value">The shelfmark as decided.</param>
    /// <returns>The shelfmark, or the reason it is not one.</returns>
    public static Result<Shelfmark> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Shelfmark>.Failure(
                HoldingsErrorCodes.InvalidShelfmark,
                "A shelfmark is required.");
        }

        var trimmed = value.Trim();

        return trimmed.Length > MaxLength
            ? Result<Shelfmark>.Failure(
                HoldingsErrorCodes.InvalidShelfmark,
                $"A shelfmark may not exceed {MaxLength} characters.")
            : Result<Shelfmark>.Success(new Shelfmark(trimmed));
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Returns the shelfmark.</summary>
    public override string ToString() => Value;
}
