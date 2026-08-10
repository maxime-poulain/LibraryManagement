using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Circulation.Domain.Loans;

// Circulation publishes facts, never instructions. It does not know that anything sends email —
// the day a channel changes, nothing here moves. Today every event feeds a projection; the ones
// Charges and Notifications will consume are already published, because an event not published
// when it happened cannot be recovered afterwards.

/// <summary>
/// A loan started.
/// </summary>
/// <param name="LoanId">The new loan.</param>
/// <param name="CopyId">The copy that went out.</param>
/// <param name="EditionId">The edition the copy belongs to.</param>
/// <param name="BorrowerId">Who took it.</param>
/// <param name="DueDate">When it is expected back.</param>
public sealed record LoanCheckedOut(
    LoanId LoanId,
    CopyId CopyId,
    EditionId EditionId,
    BorrowerId BorrowerId,
    DateOnly DueDate) : DomainEvent;

/// <summary>
/// A copy came back and its loan closed.
/// </summary>
/// <param name="LoanId">The loan that ended.</param>
/// <param name="CopyId">The copy that came back.</param>
/// <param name="BorrowerId">Who brought it.</param>
/// <param name="DaysLate">How late it was, in days the library was open — a closed day is never
/// billed, and the calendar that decides which days those are stays in this context. Zero is
/// published all the same: whether a return was late is a circulation fact; what lateness costs
/// is a money question, and Charges answers it — a price here would move the tariff into
/// lending.</param>
public sealed record LoanReturned(
    LoanId LoanId,
    CopyId CopyId,
    BorrowerId BorrowerId,
    int DaysLate) : DomainEvent;

/// <summary>
/// A copy came back spoiled — pages torn, water through the spine — and the desk said so while
/// closing the loan.
/// </summary>
/// <param name="LoanId">The loan whose return carried the observation.</param>
/// <param name="CopyId">The copy that came back worse than it went out.</param>
/// <param name="BorrowerId">Who brought it back.</param>
/// <remarks>
/// A fact beside <see cref="LoanReturned"/>, never a flag on it: the return says a copy is back
/// and how late, this says the state it is back in, and the two have different audiences — a
/// priced lateness would survive unchanged if the library stopped billing damage tomorrow. The
/// attribution is settled by construction: the observation is made at the return, with the
/// borrower standing there, so nobody later guesses whose loan spoiled the copy. What the object
/// itself becomes — worn, withdrawn, sent for rebinding — stays Holdings' record, entered by its
/// own moments.
/// </remarks>
public sealed record CopyReturnedDamaged(
    LoanId LoanId,
    CopyId CopyId,
    BorrowerId BorrowerId) : DomainEvent;

/// <summary>
/// A copy is due back soon, and the borrower has not been told yet.
/// </summary>
/// <param name="LoanId">The loan.</param>
/// <param name="CopyId">The copy to bring back.</param>
/// <param name="BorrowerId">Who has it.</param>
/// <param name="DueDate">When it is expected.</param>
/// <param name="AnyoneIsWaiting">
/// Whether somebody is queued for the edition. Carried because it decides what the message should
/// ask for — <em>renew it</em>, or <em>please bring it back</em> — and a message that says the
/// wrong one wastes a trip to the library. A fact at the moment of announcing, like every fact an
/// event carries.
/// </param>
public sealed record LoanDueSoon(
    LoanId LoanId,
    CopyId CopyId,
    BorrowerId BorrowerId,
    DateOnly DueDate,
    bool AnyoneIsWaiting) : DomainEvent;

/// <summary>
/// A copy is late, and the borrower has not been told at this stage of the schedule yet.
/// </summary>
/// <param name="LoanId">The loan.</param>
/// <param name="CopyId">The copy that has not come back.</param>
/// <param name="BorrowerId">Who has it.</param>
/// <param name="DueDate">When it was expected.</param>
/// <param name="DaysOverdue">
/// How late it actually is on the day of announcing — not the stage of the schedule that
/// triggered the message. A run that missed days announces the truth, not the appointment it is
/// catching up on. Elapsed days, deliberately, where the fine counts open ones: a message says
/// how long the copy has been kept, a fine says what the kept days cost, and the borrower's own
/// calendar agrees with the first.
/// </param>
public sealed record LoanBecameOverdue(
    LoanId LoanId,
    CopyId CopyId,
    BorrowerId BorrowerId,
    DateOnly DueDate,
    int DaysOverdue) : DomainEvent;

/// <summary>
/// A renewal was granted and the due date moved.
/// </summary>
/// <param name="LoanId">The loan renewed.</param>
/// <param name="NewDueDate">When the copy is now expected back.</param>
public sealed record RenewalGranted(LoanId LoanId, DateOnly NewDueDate) : DomainEvent;

/// <summary>
/// The library stopped waiting for a copy to come back.
/// </summary>
/// <param name="LoanId">The loan written off.</param>
/// <param name="CopyId">The copy that never came back — Holdings will mark it lost.</param>
/// <param name="BorrowerId">Who had it — Charges will price the replacement.</param>
public sealed record LoanDeclaredLost(
    LoanId LoanId,
    CopyId CopyId,
    BorrowerId BorrowerId) : DomainEvent;

/// <summary>
/// The copy of a written-off loan turned up, and the lateness it had accrued is finally known.
/// </summary>
/// <param name="LoanId">The loan, still ended — a recovery reopens nothing.</param>
/// <param name="CopyId">The copy that turned up.</param>
/// <param name="BorrowerId">Who had it.</param>
/// <param name="DaysLate">
/// Open days from the due date to the day the library stopped waiting — never to the recovery:
/// the years a book spends behind a radiator are nobody's fine, and the count froze the day the
/// loss was declared.
/// </param>
/// <remarks>
/// It leaves this context through the same contract an ordinary return does, because to the
/// reader it is the same fact — this loan's lateness, priced by whoever prices time. The
/// replacement the loss once cost is cancelled by the find on another road entirely, and this
/// context knows nothing about it.
/// </remarks>
public sealed record LoanRecovered(
    LoanId LoanId,
    CopyId CopyId,
    BorrowerId BorrowerId,
    int DaysLate) : DomainEvent;

/// <summary>
/// A loan still out now names a different edition, because Catalog merged two records into one.
/// </summary>
/// <param name="LoanId">The loan. Nothing about the loan itself changed, which is the point.</param>
/// <param name="PreviousEditionId">
/// The record that stopped answering. A projection keyed on the edition has to retract this one.
/// </param>
/// <param name="NewEditionId">The record the loan answers to from now on — and the queue with it.</param>
/// <remarks>
/// The one event here that is not this context's own observation: it reports what Circulation did in
/// answer to a fact Catalog announced. Only live loans produce it, because only a live loan will ask
/// the queue question again; an ended loan keeps the identifier it was made under.
/// </remarks>
public sealed record LoanRepointed(
    LoanId LoanId,
    EditionId PreviousEditionId,
    EditionId NewEditionId) : DomainEvent;
