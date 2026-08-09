namespace LibraryManagement.Circulation.Domain.Loans;

/// <summary>
/// Stores and retrieves <see cref="Loan"/> aggregates.
/// </summary>
/// <remarks>
/// <para>
/// Declared by the domain and implemented by the infrastructure: the domain says what it needs of
/// a store, and knows nothing of how one works.
/// </para>
/// <para>
/// There is no <c>SaveAsync</c>. A command is the unit of consistency, and a pipeline behavior
/// writes everything it changed through the module's unit of work once the handler has reported
/// success — including the return, where this aggregate and the hold queue change together, in
/// one save, deliberately.
/// </para>
/// </remarks>
public interface ILoanRepository
{
    /// <summary>
    /// Gets a loan by identity.
    /// </summary>
    /// <param name="id">The loan to get.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loan, or <see langword="null"/> when none is held under that identifier.</returns>
    ValueTask<Loan?> GetByIdAsync(LoanId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active loan carrying a copy, if any.
    /// </summary>
    /// <param name="copyId">The copy in hand.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The active loan, or <see langword="null"/> when the copy is not out.</returns>
    /// <remarks>
    /// At most one exists: a unique index over active loans holds the rule, exactly as the barcode
    /// index does in Holdings — the handler asks first so the refusal can say so, and the index is
    /// what keeps the answer true between the asking and the writing.
    /// </remarks>
    ValueTask<Loan?> ActiveLoanForCopyAsync(CopyId copyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a borrower's active loans — their half of the cap of five.
    /// </summary>
    /// <param name="borrowerId">The borrower at the desk.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>How many copies they have out.</returns>
    /// <remarks>
    /// Computed at the moment of the operation, never stored: the cap constrains the act, and a
    /// stored count would be an invariant on state. The race two desks could run is tolerated by
    /// decision — the borrower is physically standing at one of them.
    /// </remarks>
    ValueTask<int> CountActiveForBorrowerAsync(
        BorrowerId borrowerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a borrower already has a copy of an edition out.
    /// </summary>
    /// <param name="borrowerId">The borrower placing a hold.</param>
    /// <param name="editionId">The edition they would queue for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when a copy of that edition is already out to them.</returns>
    ValueTask<bool> BorrowerHasActiveLoanForEditionAsync(
        BorrowerId borrowerId,
        EditionId editionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Of the given copies, returns the ones currently out on an active loan.
    /// </summary>
    /// <param name="copyIds">Candidate copies — in practice, the lendable copies of an edition.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The subset that is out.</returns>
    /// <remarks>
    /// Half of the availability arithmetic: Holdings says which copies could be lent, this says
    /// which of them are out, and the hold queue says which are set aside. Availability is the
    /// conjunction, computed by whoever asks and stored nowhere.
    /// </remarks>
    ValueTask<IReadOnlyList<CopyId>> OnActiveLoanAmongAsync(
        IReadOnlyCollection<CopyId> copyIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active loans falling due within a span of days, both ends included.
    /// </summary>
    /// <param name="from">The first day of the span, ordinarily today.</param>
    /// <param name="to">The last day, ordinarily as far ahead as the courtesy reminder looks.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loans, possibly none.</returns>
    /// <remarks>
    /// The scheduled process's candidates, not its decisions: whether a given loan has already
    /// been told is the loan's own question, asked of the aggregate afterwards. Narrowing here and
    /// deciding there is what keeps the schedule in the domain and the dates in the store.
    /// </remarks>
    ValueTask<IReadOnlyList<Loan>> ActiveDueBetweenAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active loans whose due date has passed.
    /// </summary>
    /// <param name="today">The day the scheduled process is running.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loans, possibly none.</returns>
    /// <remarks>
    /// One question for two moments: the overdue reminders pick their stage from it, and the
    /// declaration of loss takes the ones the library has waited long enough for. The whole
    /// overdue set at library scale is a few hundred rows; the day it is not, the reading is
    /// batched, and nothing above this line changes.
    /// </remarks>
    ValueTask<IReadOnlyList<Loan>> ActiveOverdueAsync(
        DateOnly today,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the loan a recovered copy concerns: the most recently declared-lost loan carrying
    /// it, or <see langword="null"/> when no declared loss ever involved the copy.
    /// </summary>
    /// <param name="copyId">The copy that turned up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The loan, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Most recent, because a copy found, re-lent and lost again has several ended loans in its
    /// history and only the latest loss is the one a recovery settles — the earlier ones settled
    /// themselves the same way in their own time, and the aggregate's once-only recovery guard
    /// holds regardless.
    /// </remarks>
    ValueTask<Loan?> MostRecentlyDeclaredLostForCopyAsync(
        CopyId copyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a loan that just started.
    /// </summary>
    /// <param name="loan">The loan to add.</param>
    /// <remarks>
    /// Synchronous, and deliberately so: nothing is written here — the module's unit of work
    /// writes once the command has succeeded.
    /// </remarks>
    void Add(Loan loan);
}
