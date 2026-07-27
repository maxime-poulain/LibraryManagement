using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Works;

/// <summary>
/// The title a work is known by.
/// </summary>
public sealed class Title : ValueObject<Title>
{
    /// <summary>The greatest number of characters a title may run to.</summary>
    public const int MaxLength = 500;

    private Title(string value) => Value = value;

    /// <summary>Gets the title.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a title, trimming the surrounding white space.
    /// </summary>
    /// <param name="value">The title as entered.</param>
    /// <returns>The title, or the reason it is not one.</returns>
    public static Result<Title> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Title>.Failure(CatalogErrorCodes.InvalidTitle, "A title is required.");
        }

        var trimmed = value.Trim();

        return trimmed.Length > MaxLength
            ? Result<Title>.Failure(
                CatalogErrorCodes.InvalidTitle,
                $"A title may not exceed {MaxLength} characters.")
            : Result<Title>.Success(new Title(trimmed));
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <summary>Returns the title.</summary>
    public override string ToString() => Value;
}
