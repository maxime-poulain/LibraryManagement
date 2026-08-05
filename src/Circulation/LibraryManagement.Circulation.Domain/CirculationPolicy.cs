namespace LibraryManagement.Circulation.Domain;

/// <summary>
/// Every number the business can change, in one place. How many, how long, how often — and what a
/// debt forbids. It governs holds and pickup deadlines as much as loans, which is why it is not
/// called a loan policy.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The policy is data, not code.</strong> None of these values is a constant scattered
/// through an aggregate — a decision of the library must not be a deployment. The aggregates take
/// the policy as a parameter and hold the rules; the numbers ride in here. Today the one instance
/// is <see cref="Current"/> and the host registers it; the day the library wants to change a value
/// at runtime, the instance comes from a table and nothing in the domain moves.
/// </para>
/// <para>
/// One flat set of values, no table per member category: a child and an adult may borrow the same
/// five copies, by decision. Being a record with init-only values is what keeps indexing it by
/// category later an addition rather than a rewrite.
/// </para>
/// </remarks>
public sealed record CirculationPolicy
{
    /// <summary>The values as the library has decided them.</summary>
    public static readonly CirculationPolicy Current = new();

    /// <summary>
    /// Gets the most a borrower may have going at once — active loans, queued holds and holds
    /// awaiting pickup, all three counted as one number.
    /// </summary>
    /// <remarks>
    /// Named after what it counts. It was almost <c>MaxConcurrentItems</c>, and none of the three
    /// things it counts is an item: a hold is a claim on an edition, and <em>item</em> is in any
    /// case the word FRBR uses for what this model calls a copy. Blunt and unmisreadable is the
    /// whole requirement of a policy setting.
    /// </remarks>
    public int MaxLoansAndHolds { get; init; } = 5;

    /// <summary>Gets how long a loan runs, in days.</summary>
    public int LoanDurationInDays { get; init; } = 21;

    /// <summary>Gets how many times one loan may be renewed.</summary>
    public int MaxRenewals { get; init; } = 2;

    /// <summary>Gets how long a trapped copy waits on the hold shelf, in days.</summary>
    public int PickupPeriodInDays { get; init; } = 7;

    /// <summary>Gets how many days before the due date the courtesy reminder goes out.</summary>
    public int CourtesyReminderDaysBeforeDue { get; init; } = 3;

    /// <summary>Gets the days after the due date on which overdue reminders go out.</summary>
    public IReadOnlyList<int> OverdueReminderDaysAfterDue { get; init; } = [1, 7, 14];

    /// <summary>Gets how many days overdue a loan runs before the library stops waiting.</summary>
    public int DeclaredLostAfterDays { get; init; } = 30;

    /// <summary>
    /// Gets the debt above which borrowing, renewing and placing holds are forbidden. Zero — any
    /// amount owed blocks, from the first cent, by decision.
    /// </summary>
    /// <remarks>
    /// Zero is the encoding of "no threshold": raising it would be adopting the alternative the
    /// strategic design records as rejected, and it should be raised knowingly or not at all.
    /// </remarks>
    public decimal BlockingDebt { get; init; }

    /// <summary>
    /// Judges whether what a borrower owes forbids borrowing, renewing and placing holds.
    /// </summary>
    /// <param name="debt">The amount owed, as Charges states it. Circulation's word for that
    /// figure, seen as something that forbids.</param>
    /// <returns><see langword="true"/> when the debt blocks the act.</returns>
    /// <remarks>
    /// The judgement lives here, never in Charges: Charges states the amount, and if it exposed
    /// <c>IsBlocked</c> the rule would have moved into the wrong context. With no threshold and
    /// irreversible hold cancellation, twenty cents of lateness costs a queue position waited
    /// months for; that is the rule as decided, and if staff are one day seen waiving trivial
    /// fines to spare someone their place, this is the clause they are working around.
    /// </remarks>
    public bool DebtForbids(decimal debt) => debt > BlockingDebt;

    /// <summary>
    /// Judges whether a borrower's current load fills the cap.
    /// </summary>
    /// <param name="loansAndHolds">Active loans plus live holds, counted at the moment of the act.</param>
    /// <returns><see langword="true"/> when nothing more may be added.</returns>
    /// <remarks>
    /// The cap constrains the act, not the state: it is asked before something is added and never
    /// of what exists, so lowering it strands nobody — borrowers over a lowered cap are in
    /// perfect order, they borrowed under the previous rule.
    /// </remarks>
    public bool IsAtTheCap(int loansAndHolds) => loansAndHolds >= MaxLoansAndHolds;
}
