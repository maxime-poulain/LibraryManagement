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
/// <param name="DaysLate">How late it was, and zero is published all the same: whether a return
/// was late is a circulation fact; what lateness costs is a money question, and Charges answers
/// it — a price here would move the tariff into lending.</param>
public sealed record LoanReturned(
    LoanId LoanId,
    CopyId CopyId,
    BorrowerId BorrowerId,
    int DaysLate) : DomainEvent;

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
/// catching up on.
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
