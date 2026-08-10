namespace LibraryManagement.Circulation.Domain.Holds;

/// <summary>
/// One borrower's claim in one queue. Lives inside <see cref="HoldQueue"/> and never alone: "who
/// is first" is enforced by nothing if each hold is its own root.
/// </summary>
/// <remarks>
/// Not an <c>Entity&lt;T&gt;</c> of the shared kernel on purpose: that base carries the audit
/// trail, and a hold's only date the business cares about is <see cref="PlacedOn"/>, which the
/// queue's ordering rests on. The identity exists so the history projection can follow one claim
/// across the events that tell its story.
/// </remarks>
public sealed class Hold
{
    private Hold(HoldId id, BorrowerId borrowerId, DateTimeOffset placedOn)
    {
        Id = id;
        BorrowerId = borrowerId;
        PlacedOn = placedOn;
        Status = HoldStatus.Queued;
    }

    /// <summary>Gets the claim's identity.</summary>
    public HoldId Id { get; private set; }

    /// <summary>Gets whose claim it is.</summary>
    public BorrowerId BorrowerId { get; private set; }

    /// <summary>
    /// Gets the instant the claim was placed. The queue's order, which is why it is an instant
    /// and not a day: two holds on one edition in one afternoon are the ordinary case, and a date
    /// could not tell them apart.
    /// </summary>
    public DateTimeOffset PlacedOn { get; private set; }

    /// <summary>Gets where the claim stands.</summary>
    public HoldStatus Status { get; private set; }

    /// <summary>Gets the copy set aside for this claim, or <see langword="null"/> while queued.</summary>
    public CopyId? TrappedCopyId { get; private set; }

    /// <summary>Gets when the set-aside copy stops waiting, or <see langword="null"/> while queued.</summary>
    public DateOnly? PickupDeadline { get; private set; }

    /// <summary>
    /// Gets whether the borrower has been told the copy is about to go back on the shelf.
    /// </summary>
    /// <remarks>
    /// The hold shelf's own memory, and the reason the tactical design's claim that idempotence
    /// rests <em>entirely</em> on the loan's <c>RemindersSent</c> had to be amended: without this,
    /// every run of the scheduled process would announce the same imminent expiry again. A flag
    /// where the loan needs a set, because the pickup period has exactly one appointment in it —
    /// the day before the deadline — while a loan's schedule is a list.
    /// </remarks>
    public bool ExpiryWarningSent { get; private set; }

    internal static Hold PlacedBy(HoldId id, BorrowerId borrowerId, DateTimeOffset placedOn)
        => new(id, borrowerId, placedOn);

    /// <summary>
    /// The same claim, as an object the surviving queue of a merge can hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>A new instance, and the store is what requires it.</strong> A hold belongs to its
    /// queue as an owned collection, so its key is the pair of the queue's edition and its own
    /// identifier — and an owned entity cannot change owners, because its identity contains its
    /// parent's. Handing the same object to another queue is refused outright by the change tracker,
    /// not merely awkward.
    /// </para>
    /// <para>
    /// So the row moves as a deletion and an insertion, and every field the claim holds travels with
    /// it, <see cref="Id"/> included. What the borrower has is the same claim: the identifier the
    /// history projection follows, the placement instant the queue orders by, and the copy set aside
    /// for them if one was.
    /// </para>
    /// </remarks>
    internal Hold SameClaimInAnotherQueue() => new(Id, BorrowerId, PlacedOn)
    {
        Status = Status,
        TrappedCopyId = TrappedCopyId,
        PickupDeadline = PickupDeadline,
        ExpiryWarningSent = ExpiryWarningSent,
    };

    internal void Trap(CopyId copyId, DateOnly pickupDeadline)
    {
        Status = HoldStatus.AwaitingPickup;
        TrappedCopyId = copyId;
        PickupDeadline = pickupDeadline;

        // A fresh deadline is a fresh appointment: a claim offered a second copy after the first
        // one's collector cancelled must be warned about the new one too.
        ExpiryWarningSent = false;
    }

    internal void NoteExpiryWarned() => ExpiryWarningSent = true;

    internal void Release()
    {
        Status = HoldStatus.Queued;
        TrappedCopyId = null;
        PickupDeadline = null;

        // The next copy set aside for this claim is a fresh appointment, warned about afresh.
        ExpiryWarningSent = false;
    }
}
