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

    internal static Hold PlacedBy(HoldId id, BorrowerId borrowerId, DateTimeOffset placedOn)
        => new(id, borrowerId, placedOn);

    internal void Trap(CopyId copyId, DateOnly pickupDeadline)
    {
        Status = HoldStatus.AwaitingPickup;
        TrappedCopyId = copyId;
        PickupDeadline = pickupDeadline;
    }

    internal void ReleaseCopy()
    {
        Status = HoldStatus.Queued;
        TrappedCopyId = null;
        PickupDeadline = null;
    }
}
