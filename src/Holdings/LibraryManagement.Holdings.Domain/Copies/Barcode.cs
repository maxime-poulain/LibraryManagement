using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Domain.Copies;

/// <summary>
/// The label a copy is identified by at the desk. Unique in the library.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The model does not parse it.</strong> Libraries print barcodes in pre-bought ranges, in
/// whichever symbology their scanners were sold with — Codabar, EAN-13, Code 39 — and the same shelf
/// carries two generations of them. A value object validating a format would reject a legitimate
/// label the day a new batch arrives, and the refusal would surface at the desk with a reader
/// waiting. Length and non-blankness are the whole of it.
/// </para>
/// <para>
/// <strong>Uniqueness is not enforced here.</strong> A barcode cannot see the other barcodes, and a
/// value object that answered for the whole set would have to reach a store to do it. The rule is
/// held by a unique index and reported by the handler that asks first, which is the same division
/// the cap in Circulation makes for the same reason.
/// </para>
/// </remarks>
public sealed class Barcode : ValueObject<Barcode>
{
    /// <summary>The fewest characters a label may carry.</summary>
    /// <remarks>
    /// A floor rather than a format. Four is short enough to accept the hand-written labels a small
    /// library still has on its oldest stock, and long enough that a mis-scan producing one or two
    /// characters does not become a copy.
    /// </remarks>
    public const int MinLength = 4;

    /// <summary>The greatest number of characters a label may run to.</summary>
    public const int MaxLength = 32;

    private Barcode(string value) => Value = value;

    /// <summary>Gets the label.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a barcode, trimming the surrounding white space.
    /// </summary>
    /// <param name="value">The label as scanned or typed.</param>
    /// <returns>The barcode, or the reason it is not one.</returns>
    public static Result<Barcode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<Barcode>.Failure(
                HoldingsErrorCodes.InvalidBarcode,
                "A barcode is required.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length < MinLength)
        {
            return Result<Barcode>.Failure(
                HoldingsErrorCodes.InvalidBarcode,
                $"A barcode must be at least {MinLength} characters.");
        }

        return trimmed.Length > MaxLength
            ? Result<Barcode>.Failure(
                HoldingsErrorCodes.InvalidBarcode,
                $"A barcode may not exceed {MaxLength} characters.")
            : Result<Barcode>.Success(new Barcode(trimmed));
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Ordinal, so two labels differing only in case are two labels. A scanner reads what is
        // printed, and a library that prints both "a1" and "A1" has issued two barcodes.
        yield return Value;
    }

    /// <summary>Returns the label.</summary>
    public override string ToString() => Value;
}
