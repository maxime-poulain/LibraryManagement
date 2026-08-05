namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// Something a member owes, and what is left of it.
/// </summary>
/// <remarks>
/// <para>
/// An entity within <see cref="MemberAccount"/>, never reachable on its own: what may be paid, and
/// in what order, is a decision about the account as a whole, and a charge that could be settled
/// behind the account's back would put that decision outside the only boundary that can hold it.
/// </para>
/// <para>
/// <strong>The two kinds are two types</strong> — see <see cref="OverdueFine"/> and
/// <see cref="ReplacementCharge"/>. This base holds what they genuinely share, which is the money
/// and its settlement; everything that differs between charging for time and charging for an object
/// lives in the subclass or in the moment that creates it.
/// </para>
/// </remarks>
public abstract class Charge
{
    private protected Charge(
        ChargeId id,
        Money amount,
        LoanId loanId,
        CopyId copyId,
        DateOnly incurredOn)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!amount.IsPositive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "A charge of nothing is the absence of a charge, not a charge of zero.");
        }

        Id = id;
        Amount = amount;
        LoanId = loanId;
        CopyId = copyId;
        IncurredOn = incurredOn;
        Paid = Money.Zero;
    }

    // For the store, which materializes through the properties rather than through a constructor.
    private protected Charge()
    {
        Id = null!;
        Amount = null!;
        LoanId = null!;
        CopyId = null!;
        Paid = null!;
    }

    /// <summary>Identifies this charge within its account.</summary>
    public ChargeId Id { get; private set; }

    /// <summary>What was charged.</summary>
    public Money Amount { get; private set; }

    /// <summary>How much of it has been settled.</summary>
    public Money Paid { get; private set; }

    /// <summary>The loan this prices.</summary>
    public LoanId LoanId { get; private set; }

    /// <summary>The copy behind that loan.</summary>
    public CopyId CopyId { get; private set; }

    /// <summary>The day it was incurred.</summary>
    public DateOnly IncurredOn { get; private set; }

    /// <summary>What is still owed on it.</summary>
    public Money Outstanding => Money.Subtract(Amount, Paid);

    /// <summary>Whether nothing is left to pay.</summary>
    public bool IsSettled => !Outstanding.IsPositive;

    /// <summary>
    /// Settles part or all of what is left.
    /// </summary>
    /// <param name="amount">How much to settle, never more than is outstanding.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="amount"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="amount"/> exceeds what is outstanding. The account allocates and
    /// is what keeps that from happening; reaching here with too much is a mistake in the
    /// allocation, not a refusal to report to a librarian.
    /// </exception>
    internal void Settle(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (amount.Exceeds(Outstanding))
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                "A charge cannot be settled beyond what is outstanding.");
        }

        Paid = Money.Add(Paid, amount);
    }
}

/// <summary>
/// Charged for time: a copy came back after its due date.
/// </summary>
/// <remarks>
/// Small, frequent, and waived at the desk as a routine courtesy. Its amount is a rate applied to a
/// duration and capped, its occasion is a return, and it ends when it is paid — every one of which
/// differs from a <see cref="ReplacementCharge"/>, which is why a single type with a kind would
/// have merged two policies that share nothing but a column.
/// </remarks>
public sealed class OverdueFine : Charge
{
    private OverdueFine(
        ChargeId id,
        Money amount,
        LoanId loanId,
        CopyId copyId,
        DateOnly incurredOn,
        int daysLate)
        : base(id, amount, loanId, copyId, incurredOn)
        => DaysLate = daysLate;

    private OverdueFine()
    {
    }

    /// <summary>
    /// How late the copy was, kept because the amount alone cannot answer a member who disputes it.
    /// </summary>
    public int DaysLate { get; private set; }

    /// <summary>
    /// Records a fine for a copy returned late.
    /// </summary>
    /// <param name="id">The charge's identity.</param>
    /// <param name="amount">What the tariff made of the delay.</param>
    /// <param name="loanId">The loan returned late.</param>
    /// <param name="copyId">The copy behind it.</param>
    /// <param name="incurredOn">The day the return was recorded.</param>
    /// <param name="daysLate">Days past the due date, as Circulation counted them.</param>
    /// <returns>The fine.</returns>
    public static OverdueFine For(
        ChargeId id,
        Money amount,
        LoanId loanId,
        CopyId copyId,
        DateOnly incurredOn,
        int daysLate)
        => new(id, amount, loanId, copyId, incurredOn, daysLate);
}

/// <summary>
/// Charged for an object: a copy that will not come back.
/// </summary>
/// <remarks>
/// Large, rare, and a decision somebody signs for. Unlike a fine it has a second ending — the day
/// the book turns up — which is why <see cref="Charge.CopyId"/> is recorded: the fact that undoes
/// this comes from Holdings, and Holdings speaks copies.
/// </remarks>
public sealed class ReplacementCharge : Charge
{
    private ReplacementCharge(
        ChargeId id,
        Money amount,
        LoanId loanId,
        CopyId copyId,
        DateOnly incurredOn)
        : base(id, amount, loanId, copyId, incurredOn)
    {
    }

    private ReplacementCharge()
    {
    }

    /// <summary>
    /// Records what a copy that will not come back costs.
    /// </summary>
    /// <param name="id">The charge's identity.</param>
    /// <param name="amount">The replacement cost.</param>
    /// <param name="loanId">The loan given up on.</param>
    /// <param name="copyId">The copy that never came back.</param>
    /// <param name="incurredOn">The day the library stopped waiting.</param>
    /// <returns>The charge.</returns>
    public static ReplacementCharge For(
        ChargeId id,
        Money amount,
        LoanId loanId,
        CopyId copyId,
        DateOnly incurredOn)
        => new(id, amount, loanId, copyId, incurredOn);
}
