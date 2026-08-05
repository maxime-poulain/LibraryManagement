namespace LibraryManagement.Circulation.Domain.Holds;

/// <summary>
/// Stores and retrieves <see cref="HoldQueue"/> aggregates.
/// </summary>
/// <remarks>
/// There is no <c>SaveAsync</c>. A command is the unit of consistency, and a pipeline behavior
/// writes everything it changed through the module's unit of work once the handler has reported
/// success.
/// </remarks>
public interface IHoldQueueRepository
{
    /// <summary>
    /// Gets the queue for an edition, live holds included.
    /// </summary>
    /// <param name="editionId">The edition.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The queue, or <see langword="null"/> when no claim has ever opened one.</returns>
    ValueTask<HoldQueue?> GetByEditionAsync(
        EditionId editionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a borrower's live holds across every queue — queued and awaiting pickup alike,
    /// their other half of the cap of five.
    /// </summary>
    /// <param name="borrowerId">The borrower at the desk.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>How many claims they have going.</returns>
    /// <remarks>
    /// A trapped copy waiting on the hold shelf is immobilized for that borrower, so it occupies
    /// a place exactly as a borrowed one does — which is why both statuses count.
    /// </remarks>
    ValueTask<int> CountLiveForBorrowerAsync(
        BorrowerId borrowerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the queues holding a set-aside copy whose pickup deadline falls on or before a day.
    /// </summary>
    /// <param name="lastDeadline">The furthest deadline of interest.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The queues, live holds included, possibly none.</returns>
    /// <remarks>
    /// One question for both hold moments of the scheduled process: the warning asks about
    /// tomorrow, the expiry about yesterday, and each aggregate decides which of its own claims
    /// the day concerns.
    /// </remarks>
    ValueTask<IReadOnlyList<HoldQueue>> WithHoldsAwaitingPickupThroughAsync(
        DateOnly lastDeadline,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a queue opened by its first claim.
    /// </summary>
    /// <param name="queue">The queue to add.</param>
    void Add(HoldQueue queue);

    /// <summary>
    /// Finds every queue in which a borrower holds a live claim.
    /// </summary>
    /// <param name="borrowerId">The borrower.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The queues, with their claims.</returns>
    /// <remarks>
    /// The one query that starts from a borrower rather than from an edition, and it exists for one
    /// moment: a debt arrives and every place that borrower occupies has to go. The index on the
    /// borrower — the same one the cap counts through — is what keeps it from being a scan.
    /// </remarks>
    ValueTask<IReadOnlyList<HoldQueue>> WithHoldsForBorrowerAsync(
        BorrowerId borrowerId,
        CancellationToken cancellationToken = default);
}
