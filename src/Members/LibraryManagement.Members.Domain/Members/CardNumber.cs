using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// The number on the card a member presents at the desk. Unique in the library.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The model does not parse it.</strong> Cards are pre-printed in purchased batches, in
/// whatever numbering the supplier used, and two generations of them circulate at once. A value
/// object validating a format would reject a legitimate card the day a new batch arrives, at the
/// desk, with a member waiting — the same argument, word for word, that keeps Holdings from
/// parsing a barcode. Length and non-blankness are the whole of it.
/// </para>
/// <para>
/// <strong>Uniqueness is not enforced here.</strong> A card number cannot see the other card
/// numbers, and a value object that answered for the whole set would have to reach a store to do
/// it. The rule is held by a unique index and reported by the handler that asks first.
/// </para>
/// </remarks>
public sealed class CardNumber : ValueObject<CardNumber>
{
    /// <summary>The fewest characters a card number may carry.</summary>
    /// <remarks>
    /// A floor rather than a format: short enough for the hand-numbered cards a small library
    /// still has in circulation, long enough that a mis-scan producing one or two characters does
    /// not become a member.
    /// </remarks>
    public const int MinLength = 4;

    /// <summary>The greatest number of characters a card number may run to.</summary>
    public const int MaxLength = 32;

    private CardNumber(string value) => Value = value;

    /// <summary>Gets the number.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a card number, trimming the surrounding white space.
    /// </summary>
    /// <param name="value">The number as scanned or typed.</param>
    /// <returns>The card number, or the reason it is not one.</returns>
    public static Result<CardNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<CardNumber>.Failure(
                MembersErrorCodes.InvalidCardNumber,
                "A card number is required.");
        }

        var trimmed = value.Trim();

        if (trimmed.Length < MinLength)
        {
            return Result<CardNumber>.Failure(
                MembersErrorCodes.InvalidCardNumber,
                $"A card number must be at least {MinLength} characters.");
        }

        return trimmed.Length > MaxLength
            ? Result<CardNumber>.Failure(
                MembersErrorCodes.InvalidCardNumber,
                $"A card number may not exceed {MaxLength} characters.")
            : Result<CardNumber>.Success(new CardNumber(trimmed));
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Ordinal, so two numbers differing only in case are two numbers — a scanner reads what is
        // printed, exactly as it does a barcode.
        yield return Value;
    }

    /// <summary>Returns the number.</summary>
    public override string ToString() => Value;
}
