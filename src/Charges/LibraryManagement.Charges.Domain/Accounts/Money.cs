using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// An amount of money, in the currency this library counts in.
/// </summary>
/// <remarks>
/// <para>
/// <strong>No currency field.</strong> One library, one currency — and a field that always says the
/// same thing is a field nobody validates and everybody trusts. The first genuinely multi-currency
/// requirement would rewrite this arithmetic rather than fill a field in, so carrying one now would
/// buy a pretence instead of a capability.
/// </para>
/// <para>
/// <strong><see cref="decimal"/> and never <c>double</c>.</strong> Money is counted in hundredths,
/// and binary floating point cannot represent a fifth of a euro; a tariff of 0.20 accumulated over a
/// fortnight would drift where anyone can see it. Two decimal places, rounded half away from zero —
/// what a till does, and what someone checking the arithmetic by hand expects.
/// </para>
/// <para>
/// <strong>It never crosses a boundary.</strong> The port Circulation declared answers with a
/// <see cref="decimal"/>, because a published language traffics in primitives. This type stays
/// inside the context, where its arithmetic is worth having.
/// </para>
/// </remarks>
public sealed class Money : ValueObject<Money>
{
    private Money(decimal amount) => Amount = amount;

    /// <summary>Nothing owed, and the identity of every addition below.</summary>
    public static Money Zero { get; } = new(0m);

    /// <summary>The amount, always to two decimal places and never negative.</summary>
    public decimal Amount { get; }

    /// <summary>Whether this is more than nothing.</summary>
    public bool IsPositive => Amount > 0m;

    /// <summary>
    /// Creates an amount, rounded to the hundredth.
    /// </summary>
    /// <param name="amount">The amount. Zero is allowed; a negative amount is not.</param>
    /// <returns>The amount.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="amount"/> is negative.</exception>
    /// <remarks>
    /// Throwing rather than answering a failed result, the same call every value object in this
    /// solution makes for a constraint no user input can violate: an amount reaches here from a
    /// tariff and a day count, never from a text box, so a negative one is a programming mistake
    /// and not something a librarian can be told about.
    /// </remarks>
    public static Money Of(decimal amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        return new Money(Math.Round(amount, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>Adds two amounts.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns>Their sum.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <remarks>
    /// A named method and not an operator, which is what every value object in this solution does.
    /// Money is the one type where arithmetic operators would genuinely read better, and declaring
    /// them obliges this class to redeclare the equality its own base already supplies — ceremony
    /// that would exist nowhere else here, to save a few characters at five call sites.
    /// </remarks>
    public static Money Add(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new Money(left.Amount + right.Amount);
    }

    /// <summary>Subtracts one amount from another.</summary>
    /// <param name="left">The amount to subtract from.</param>
    /// <param name="right">The amount to subtract.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="right"/> exceeds <paramref name="left"/>. A negative amount is not
    /// representable, and a subtraction that would produce one is a rule broken upstream — an
    /// overpayment refused, a charge settled twice — which the caller must have prevented.
    /// </exception>
    public static Money Subtract(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return Of(left.Amount - right.Amount);
    }

    /// <summary>Whether this amount is greater than another.</summary>
    /// <param name="other">The amount to compare against.</param>
    /// <returns><see langword="true"/> when this one is the larger.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    public bool Exceeds(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Amount > other.Amount;
    }

    /// <summary>The smaller of two amounts.</summary>
    /// <param name="left">The first amount.</param>
    /// <param name="right">The second amount.</param>
    /// <returns>Whichever is smaller.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    public static Money Lesser(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left.Amount <= right.Amount ? left : right;
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
    }

    /// <inheritdoc/>
    public override string ToString() => Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}
