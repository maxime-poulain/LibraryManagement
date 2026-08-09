namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// The account's memory that a loan has been priced, per kind of charge — a ledger of
/// identifiers, never of money.
/// </summary>
/// <remarks>
/// <para>
/// The whole of this context's redelivery guarantee. The first design read idempotence off the
/// outstanding charges, and the guarantee quietly expired the moment a charge ended: a charge
/// paid, waived or cancelled by a found copy leaves the account (§2 of the tactical design), so a
/// contract replayed after the desk had settled it found nothing and billed the member again —
/// precisely the window at-least-once delivery promises to hit.
/// </para>
/// <para>
/// Per kind, because a fine and a replacement charge for one loan are two legitimate charges —
/// and the kind is a string naming the charge type, so the day a third kind of charge exists its
/// memory is a new constant rather than a new shape.
/// </para>
/// </remarks>
public sealed class PricedLoan
{
    /// <summary>The memory that a loan's lateness was priced.</summary>
    public const string FineKind = nameof(OverdueFine);

    /// <summary>The memory that a loan's unreturned copy was priced.</summary>
    public const string ReplacementKind = nameof(ReplacementCharge);

    /// <summary>The memory that the damage a loan brought back was priced.</summary>
    public const string DamageKind = nameof(DamageCharge);

    // For the store.
    private PricedLoan()
    {
    }

    internal PricedLoan(LoanId loanId, string kind)
    {
        LoanId = loanId;
        Kind = kind;
    }

    /// <summary>Gets the loan that was priced.</summary>
    public LoanId LoanId { get; private set; } = null!;

    /// <summary>Gets which kind of charge priced it.</summary>
    public string Kind { get; private set; } = null!;
}
