namespace LibraryManagement.Circulation.Application.Borrowers.GetBorrowerFile;

/// <summary>
/// A borrower's circulation file: what is out, what is queued, and the verdict on their debt.
/// </summary>
/// <param name="BorrowerId">The borrower.</param>
/// <param name="Loans">What they still have to answer for, most recently checked out first.</param>
/// <param name="Holds">What they are waiting for, oldest claim first.</param>
/// <param name="DebtBlocksBorrowing"><c>Standing</c>, the judgement the glossary reserves that word
/// for: whether what this borrower owes forbids borrowing, renewing and placing holds.</param>
/// <remarks>
/// <para>
/// <strong>The verdict travels, never the amount.</strong> The glossary is strict at exactly this
/// seam — Charges says <c>Balance</c> and states a figure; this context says <c>Debt</c> and says
/// what the figure forbids. A file carrying the euros would invite its reader to re-derive the
/// consequence, and a rule re-derived at the edge is a rule no module's invariants cover. The
/// amount is on the page too, and it is Charges that puts it there.
/// </para>
/// <para>
/// <strong>Returned loans are deliberately absent.</strong> The file answers what a borrower must
/// still answer for — on loan, or declared lost and not yet resolved. A borrower's whole history
/// is the one thing in this system that grows without bound, and whether it is kept forever or
/// archived is an open question the strategic design still holds; putting it behind a desk screen
/// would answer that question by accident, in the direction that is hardest to undo.
/// </para>
/// <para>
/// The name says <c>Borrower</c> and not <c>Member</c> throughout, because this context sees a
/// person as an identity, a load and a standing and refuses the rest. That the two identifiers
/// carry the same value is a fact about the model, not a correspondence anyone should look up.
/// </para>
/// </remarks>
public sealed record BorrowerFileDto(
    Guid BorrowerId,
    IReadOnlyList<LoanOnFileDto> Loans,
    IReadOnlyList<HoldOnFileDto> Holds,
    bool DebtBlocksBorrowing);

/// <summary>
/// One loan a borrower has still to answer for.
/// </summary>
/// <param name="LoanId">The loan.</param>
/// <param name="CopyId">The copy on loan.</param>
/// <param name="EditionId">The edition it is a copy of — the queue it answers to on its way back.</param>
/// <param name="CheckedOutOn">The day it left the desk.</param>
/// <param name="DueDate">The day it is due, as renewals have left it.</param>
/// <param name="RenewalCount">How many renewals it has had.</param>
/// <param name="Status">Its status, under the name the model gives it, never its number.</param>
/// <param name="DeclaredLostOn">The day it was given up on, when it was.</param>
public sealed record LoanOnFileDto(
    Guid LoanId,
    Guid CopyId,
    Guid EditionId,
    DateOnly CheckedOutOn,
    DateOnly DueDate,
    int RenewalCount,
    string Status,
    DateOnly? DeclaredLostOn);

/// <summary>
/// One claim a borrower has on an edition.
/// </summary>
/// <param name="HoldId">The hold.</param>
/// <param name="EditionId">The edition claimed — a hold is placed on the edition, and any copy of
/// it satisfies the claim.</param>
/// <param name="Status">Whether it is still queued or a copy is waiting to be collected.</param>
/// <param name="PlacedOn">When the claim was made. The queue is served in this order.</param>
/// <param name="TrappedCopyId">The copy set aside for it, once one has been.</param>
/// <param name="PickupDeadline">The day the copy goes back on the shelf if nobody comes.</param>
public sealed record HoldOnFileDto(
    Guid HoldId,
    Guid EditionId,
    string Status,
    DateTimeOffset PlacedOn,
    Guid? TrappedCopyId,
    DateOnly? PickupDeadline);
