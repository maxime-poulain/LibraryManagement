using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Domain.Loans;

/// <summary>
/// One copy, held by one borrower, until a date. The root of everything that happens to a copy
/// while it is out.
/// </summary>
/// <remarks>
/// <para>
/// It holds identifiers from three other contexts — the copy from Holdings, the borrower from
/// Members, the edition from Catalog — and nothing else of any of them. What the copy is, who the
/// person is, what the book is called: none of it is this context's to know.
/// </para>
/// <para>
/// The edition is recorded at checkout even though the copy implies it, because the loan needs to
/// name the hold queue its copy feeds — at return, and at every renewal — without asking Holdings
/// again. It is the edition <em>as it stood when the loan started</em>, which is exactly the queue
/// this copy played in.
/// </para>
/// </remarks>
public sealed class Loan : AggregateRoot<LoanId>
{
    private Loan(
        LoanId id,
        CopyId copyId,
        EditionId editionId,
        BorrowerId borrowerId,
        DateOnly checkedOutOn,
        DateOnly dueDate) : base(id)
    {
        CopyId = copyId;
        EditionId = editionId;
        BorrowerId = borrowerId;
        CheckedOutOn = checkedOutOn;
        DueDate = dueDate;
    }

    /// <summary>Gets the copy that is out. Identity only, never the copy.</summary>
    public CopyId CopyId { get; }

    /// <summary>Gets the edition the copy belongs to — the hold queue this loan answers to.</summary>
    public EditionId EditionId { get; }

    /// <summary>Gets who has it. Identity only, never the member.</summary>
    public BorrowerId BorrowerId { get; }

    /// <summary>Gets the day the loan started.</summary>
    public DateOnly CheckedOutOn { get; }

    /// <summary>Gets the day the copy is expected back, inclusive.</summary>
    public DateOnly DueDate { get; private set; }

    /// <summary>Gets how many times the loan has been renewed.</summary>
    public int RenewalCount { get; private set; }

    /// <summary>Gets the day the copy came back, or <see langword="null"/> while it is out.</summary>
    public DateOnly? ReturnedOn { get; private set; }

    /// <summary>Gets where the loan stands.</summary>
    public LoanStatus Status { get; private set; }

    /// <summary>
    /// Starts a loan.
    /// </summary>
    /// <param name="id">The identifier the loan will keep for its whole life.</param>
    /// <param name="copyId">The copy going out. That it may go is the caller's rule to enforce.</param>
    /// <param name="editionId">The edition the copy belongs to, as Holdings answered it.</param>
    /// <param name="borrowerId">Who takes it. That they may is the caller's rule to enforce.</param>
    /// <param name="today">The day of the checkout. The domain has no clock; the caller does.</param>
    /// <param name="policy">The circulation policy, which decides the due date.</param>
    /// <returns>The new loan.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any reference argument is null.</exception>
    /// <remarks>
    /// Returns a <see cref="Loan"/> and not a <c>Result</c>, for the reason <c>Copy.Acquire</c>
    /// gives: every rule that could refuse a checkout spans more than this aggregate — standing,
    /// the cap, lendability, the queue — and none can be answered from inside a loan, so the
    /// handler asks them all first.
    /// </remarks>
    public static Loan CheckOut(
        LoanId id,
        CopyId copyId,
        EditionId editionId,
        BorrowerId borrowerId,
        DateOnly today,
        CirculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(copyId);
        ArgumentNullException.ThrowIfNull(editionId);
        ArgumentNullException.ThrowIfNull(borrowerId);
        ArgumentNullException.ThrowIfNull(policy);

        var loan = new Loan(
            id, copyId, editionId, borrowerId, today, today.AddDays(policy.LoanDurationInDays));

        loan.AddDomainEvent(new LoanCheckedOut(id, copyId, editionId, borrowerId, loan.DueDate));

        return loan;
    }

    /// <summary>
    /// Moves the due date forward without returning the copy.
    /// </summary>
    /// <param name="policy">The circulation policy, which decides the extension and the limit.</param>
    /// <returns>Success, or the reason the renewal was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The new due date is the old one plus the loan duration — the arithmetic the membership
    /// renewal already decided, for the same reason: renewing early costs nothing, or borrowers
    /// would learn to renew at the last minute. Being at the cap does not prevent a renewal;
    /// nothing is added.
    /// </para>
    /// <para>
    /// This aggregate refuses the two conditions it can see — an inactive loan, an exhausted
    /// limit. The other two of the design's four — the borrower's standing, the empty queue — span
    /// other aggregates and other contexts, and the handler asks them.
    /// </para>
    /// </remarks>
    public Result Renew(CirculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (Status != LoanStatus.Active)
        {
            return Result.Failure(
                CirculationErrorCodes.LoanNotActive,
                $"A loan that is {Describe(Status)} cannot be renewed.");
        }

        if (RenewalCount >= policy.MaxRenewals)
        {
            return Result.Failure(
                CirculationErrorCodes.RenewalLimitReached,
                $"A loan may be renewed at most {policy.MaxRenewals} times.");
        }

        RenewalCount++;
        DueDate = DueDate.AddDays(policy.LoanDurationInDays);

        AddDomainEvent(new RenewalGranted(Id, DueDate));

        return Result.Success();
    }

    /// <summary>
    /// Ends the loan by the copy coming back. Lateness is computed and recorded, and published
    /// even when it is zero — Charges decides there is nothing to charge, not this context.
    /// </summary>
    /// <param name="returnedOn">The day of the return.</param>
    /// <returns>Success, or the reason the return was refused.</returns>
    /// <remarks>
    /// A borrower may always return: no standing, no cap, no queue is consulted. A block that
    /// prevented returning would create the opposite incentive to the one intended — and it is
    /// often the return itself that settles the debt.
    /// </remarks>
    public Result Return(DateOnly returnedOn)
    {
        if (Status != LoanStatus.Active)
        {
            return Result.Failure(
                CirculationErrorCodes.LoanNotActive,
                $"A loan that is {Describe(Status)} has nothing out to bring back.");
        }

        ReturnedOn = returnedOn;
        Status = LoanStatus.Returned;

        var daysLate = Math.Max(0, returnedOn.DayNumber - DueDate.DayNumber);

        AddDomainEvent(new LoanReturned(Id, CopyId, BorrowerId, daysLate));

        return Result.Success();
    }

    /// <summary>
    /// Ends the loan by decision rather than by a return: the library stops waiting.
    /// </summary>
    /// <returns>Success, or the reason the loan could not be declared lost.</returns>
    /// <remarks>
    /// Terminal, and deliberately not undone by the copy turning up: finding it afterwards starts
    /// a return of a copy Holdings had marked lost, never a reopening. Raised by the scheduled
    /// process after the policy's waiting period; the method exists here so that process finds
    /// its route already built.
    /// </remarks>
    public Result DeclareLost()
    {
        if (Status == LoanStatus.Returned)
        {
            return Result.Failure(
                CirculationErrorCodes.LoanNotActive,
                "A loan whose copy came back cannot be declared lost.");
        }

        if (Status == LoanStatus.DeclaredLost)
        {
            return Result.Success();
        }

        Status = LoanStatus.DeclaredLost;

        AddDomainEvent(new LoanDeclaredLost(Id, CopyId, BorrowerId));

        return Result.Success();
    }

    private static string Describe(LoanStatus status) => status switch
    {
        LoanStatus.Active => "active",
        LoanStatus.Returned => "returned",
        _ => "declared lost",
    };
}
