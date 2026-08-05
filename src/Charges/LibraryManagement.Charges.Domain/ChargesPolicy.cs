using LibraryManagement.Charges.Domain.Accounts;

namespace LibraryManagement.Charges.Domain;

/// <summary>
/// Every number the library can change about what it charges.
/// </summary>
/// <param name="FinePerDayOverdue">What one day past the due date costs.</param>
/// <param name="GracePeriodInDays">
/// Days late that cost nothing. Zero today — a lever set to nothing rather than an absent concept,
/// so introducing one is a change of data and not of model.
/// </param>
/// <param name="MaxFinePerLoan">
/// The ceiling on an overdue fine. A fine is charged for time and time is unbounded: without this, a
/// copy returned two years late would owe more than replacing it, which no library charges and no
/// member would pay. It says nothing about a replacement charge.
/// </param>
/// <param name="ReplacementCharge">
/// What a copy that will not come back costs. One flat figure for a paperback and for a folio,
/// because no context records what a copy was worth — an acquisition price is a Holdings fact, and
/// the day it exists this becomes a default rather than the answer.
/// </param>
/// <remarks>
/// <para>
/// One place, for Circulation's reason: a decision of the library must not be a deployment. These
/// numbers move more often than any other context's — an amnesty, an exemption, a municipal
/// decision on the tariff — which is what makes the object worth more here than the single lever
/// Members has.
/// </para>
/// <para>
/// No table per member category: a child and an adult are fined the same, exactly as they may borrow
/// the same five copies. It stays a record rather than four constants so that indexing it by
/// category later is an addition rather than a rewrite.
/// </para>
/// </remarks>
public sealed record ChargesPolicy(
    decimal FinePerDayOverdue,
    int GracePeriodInDays,
    decimal MaxFinePerLoan,
    decimal ReplacementCharge)
{
    /// <summary>
    /// What the library charges today.
    /// </summary>
    public static ChargesPolicy Current { get; } = new(
        FinePerDayOverdue: 0.20m,
        GracePeriodInDays: 0,
        MaxFinePerLoan: 10.00m,
        ReplacementCharge: 25.00m);

    /// <summary>
    /// What a loan returned this many days late costs.
    /// </summary>
    /// <param name="daysLate">Days past the due date, as Circulation counted them.</param>
    /// <returns>The fine, which is <see cref="Money.Zero"/> when nothing is owed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="daysLate"/> is negative.</exception>
    /// <remarks>
    /// The whole tariff of an overdue fine, in one place rather than in the aggregate: the grace
    /// period is subtracted first, the cap applied last, and a return within the grace period costs
    /// nothing rather than a negative amount.
    /// </remarks>
    public Money FineFor(int daysLate)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(daysLate);

        var chargeable = daysLate - GracePeriodInDays;

        return chargeable <= 0
            ? Money.Zero
            : Money.Lesser(Money.Of(chargeable * FinePerDayOverdue), Money.Of(MaxFinePerLoan));
    }

    /// <summary>What a copy that will not come back costs.</summary>
    public Money ReplacementCost => Money.Of(ReplacementCharge);
}
