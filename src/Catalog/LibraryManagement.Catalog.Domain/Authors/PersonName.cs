using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Authors;

/// <summary>
/// A name under which a person is catalogued, in the form a heading takes — family name first, as in
/// <c>"Saint-Exupéry, Antoine de"</c>.
/// </summary>
/// <remarks>
/// Deliberately one string rather than separate family and given parts. A catalogue heading is a
/// single ordered string by design, and it has to accommodate names that do not divide that way at
/// all: mononyms, sovereigns, corporate bodies, names in scripts with no such distinction. Splitting
/// it would force every one of those into a shape they do not have.
/// </remarks>
public sealed class PersonName : ValueObject<PersonName>
{
    /// <summary>The greatest number of characters a heading may run to.</summary>
    public const int MaxLength = 200;

    private PersonName(string value) => Value = value;

    /// <summary>Gets the name.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a name, trimming the surrounding white space.
    /// </summary>
    /// <param name="value">The name as entered.</param>
    /// <returns>The name, or the reason it is not one.</returns>
    public static Result<PersonName> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<PersonName>.Failure(
                CatalogErrorCodes.InvalidPersonName,
                "A name is required.");
        }

        var trimmed = value.Trim();

        return trimmed.Length > MaxLength
            ? Result<PersonName>.Failure(
                CatalogErrorCodes.InvalidPersonName,
                $"A name may not exceed {MaxLength} characters.")
            : Result<PersonName>.Success(new PersonName(trimmed));
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Ordinal, so two headings differing only in case are two headings. A catalogue that treated
        // them as one would silently merge "de Beauvoir" and "De Beauvoir", which are different
        // filing decisions a librarian makes on purpose.
        yield return Value;
    }

    /// <summary>Returns the name.</summary>
    public override string ToString() => Value;
}
