using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Authors;

/// <summary>
/// One form of a name a record is catalogued under, in filing order — family name first, as in
/// <c>"Saint-Exupéry, Antoine de"</c>.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately one string rather than separate family and given parts. A name form is a single
/// ordered string by design, and it has to accommodate names that do not divide that way at all:
/// mononyms, sovereigns, corporate bodies, names in scripts with no such distinction. Splitting it
/// would force every one of those into a shape they do not have.
/// </para>
/// <para>
/// Which is why the type is not called <c>PersonName</c>, as it once was. The sentence above lists
/// what it carries, and a corporate body's name is not a person's under any reading. A form is
/// either the one a record is filed under or one of the variants that lead back to it; the type is
/// the same either way, and the role belongs to the property that holds it, not to the value.
/// </para>
/// </remarks>
public sealed class NameForm : ValueObject<NameForm>
{
    /// <summary>The greatest number of characters a name form may run to.</summary>
    public const int MaxLength = 200;

    private NameForm(string value) => Value = value;

    /// <summary>Gets the name.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a name, trimming the surrounding white space.
    /// </summary>
    /// <param name="value">The name as entered.</param>
    /// <returns>The name, or the reason it is not one.</returns>
    public static Result<NameForm> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<NameForm>.Failure(
                CatalogErrorCodes.InvalidName,
                "A name is required.");
        }

        var trimmed = value.Trim();

        return trimmed.Length > MaxLength
            ? Result<NameForm>.Failure(
                CatalogErrorCodes.InvalidName,
                $"A name may not exceed {MaxLength} characters.")
            : Result<NameForm>.Success(new NameForm(trimmed));
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Ordinal, so two name forms differing only in case are two forms. A catalogue that treated
        // them as one would silently merge "de Beauvoir" and "De Beauvoir", which are different
        // filing decisions a librarian makes on purpose.
        yield return Value;
    }

    /// <summary>Returns the name.</summary>
    public override string ToString() => Value;
}
