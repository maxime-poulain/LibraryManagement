using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Domain.Holds;

/// <summary>
/// The ordered claims on one edition. One queue per edition, never per copy: a borrower waiting
/// for <em>Le Petit Prince</em> wants the next copy, not a particular volume.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The queue is the aggregate, not the individual hold.</strong> If each hold were its own
/// root carrying a position, "who is first" would be enforced by nothing: two returns on the same
/// edition could promote two people to the front, or trap two copies for the same one. Removing a
/// hold from the middle must close the gap behind it in the same breath, and only the queue can.
/// The cost is one lock per edition; at library scale — holds arrive a few per minute, not a few
/// per millisecond — that is invisible.
/// </para>
/// <para>
/// <strong>A hold that ends leaves the aggregate.</strong> Fulfilled, expired or cancelled, its
/// outcome is published as an event and kept by a history projection; the queue holds only what
/// its invariants govern, and every invariant concerns live holds. The queue is loaded on every
/// return of its edition — the most frequent operation of the day — and an aggregate that kept
/// its own past would grow without bound precisely on the hottest path.
/// </para>
/// </remarks>
public sealed class HoldQueue : AggregateRoot<EditionId>
{
    private readonly List<Hold> _holds = [];

    private HoldQueue(EditionId id) : base(id)
    {
    }

    /// <summary>Gets the live holds — queued and awaiting pickup, nothing else.</summary>
    public IReadOnlyList<Hold> Holds => _holds.AsReadOnly();

    /// <summary>Gets whether anyone is waiting for a copy — what a renewal asks.</summary>
    /// <remarks>
    /// Queued holds only. A hold awaiting pickup already has its copy on the hold shelf: refusing
    /// a renewal for its sake would serve nobody, and the rule exists so the queue turns for the
    /// people still waiting.
    /// </remarks>
    public bool AnyoneIsWaiting => _holds.Any(hold => hold.Status == HoldStatus.Queued);

    /// <summary>Returns the borrowers currently queued, oldest claim first.</summary>
    public IReadOnlyList<BorrowerId> QueuedBorrowersInOrder() => _holds
        .Where(hold => hold.Status == HoldStatus.Queued)
        .OrderBy(hold => hold.PlacedOn)
        .Select(hold => hold.BorrowerId)
        .ToList();

    /// <summary>Returns the copies set aside on the hold shelf for this edition.</summary>
    public IReadOnlyList<CopyId> TrappedCopyIds() => _holds
        .Where(hold => hold.TrappedCopyId is not null)
        .Select(hold => hold.TrappedCopyId!)
        .ToList();

    /// <summary>
    /// Opens the queue for an edition. A queue exists from the first claim on; there is no event,
    /// because an empty queue is not a fact anyone reacts to.
    /// </summary>
    /// <param name="editionId">The edition the queue serves.</param>
    /// <returns>The empty queue.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="editionId"/> is null.</exception>
    public static HoldQueue For(EditionId editionId)
    {
        ArgumentNullException.ThrowIfNull(editionId);

        return new HoldQueue(editionId);
    }

    /// <summary>
    /// Places a claim at the end of the queue.
    /// </summary>
    /// <param name="holdId">The identifier the claim will keep, into the history that outlives it.</param>
    /// <param name="borrowerId">Whose claim it is.</param>
    /// <param name="placedOn">The instant of the claim — the position, forever.</param>
    /// <returns>Success, or the reason the claim was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
    /// <remarks>
    /// This aggregate refuses the one condition it can see: a borrower appears at most once in a
    /// queue. The other refusals of the design — a copy on the shelf, an edition already borrowed,
    /// a debt, the cap — span other aggregates and other contexts, and the handler asks them.
    /// </remarks>
    public Result PlaceHold(HoldId holdId, BorrowerId borrowerId, DateTimeOffset placedOn)
    {
        ArgumentNullException.ThrowIfNull(holdId);
        ArgumentNullException.ThrowIfNull(borrowerId);

        if (_holds.Any(hold => hold.BorrowerId == borrowerId))
        {
            return Result.Failure(
                CirculationErrorCodes.HoldAlreadyPlaced,
                "This borrower already waits in this queue; a borrower appears at most once.");
        }

        _holds.Add(Hold.PlacedBy(holdId, borrowerId, placedOn));

        AddDomainEvent(new HoldPlaced(Id, holdId, borrowerId));

        return Result.Success();
    }

    /// <summary>
    /// Sets a returned or released copy aside for the oldest queued claim whose borrower is not
    /// blocked, if anyone qualifies.
    /// </summary>
    /// <param name="copyId">The copy in hand, just returned or just released.</param>
    /// <param name="pickupDeadline">How long it waits on the hold shelf.</param>
    /// <param name="skipping">
    /// Borrowers to pass over — the ones a debt blocks, as the caller judged from the balance.
    /// Skipped, never removed: the debt event may simply not have arrived yet, and the removal is
    /// that handler's job when it does.
    /// </param>
    /// <returns>The hold now awaiting pickup, or <see langword="null"/> when nobody qualifies and
    /// the copy goes back to the shelf.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the copy is already set aside for
    /// a claim in this queue — a copy belongs to exactly one hold, and offering one twice is a
    /// defect in the caller, not a refusal to report politely.</exception>
    /// <remarks>
    /// Promotion order lives here, inside the aggregate: the oldest queued claim is served first,
    /// whatever the caller knows. Several claims may be awaiting pickup at once — three copies
    /// returning on one morning trap for the first three in the queue — and that is ordinary.
    /// </remarks>
    public Hold? TrapOldestQueued(
        CopyId copyId,
        DateOnly pickupDeadline,
        IReadOnlySet<BorrowerId> skipping)
    {
        ArgumentNullException.ThrowIfNull(copyId);
        ArgumentNullException.ThrowIfNull(skipping);

        if (_holds.Any(hold => copyId.Equals(hold.TrappedCopyId)))
        {
            throw new InvalidOperationException(
                $"Copy '{copyId}' is already set aside for a claim in this queue; a trapped copy "
                + "belongs to exactly one hold.");
        }

        var next = _holds
            .Where(hold => hold.Status == HoldStatus.Queued && !skipping.Contains(hold.BorrowerId))
            .OrderBy(hold => hold.PlacedOn)
            .FirstOrDefault();

        if (next is null)
        {
            return null;
        }

        next.Trap(copyId, pickupDeadline);

        AddDomainEvent(new HoldReadyForPickup(Id, next.Id, next.BorrowerId, copyId, pickupDeadline));

        return next;
    }

    /// <summary>
    /// Gets the hold a copy is set aside for, if any — what a checkout consults before handing a
    /// returned copy to a walk-in.
    /// </summary>
    /// <param name="copyId">The copy in hand.</param>
    /// <returns>The claim it is set aside for, or <see langword="null"/>.</returns>
    public Hold? HoldAwaitingPickupOf(CopyId copyId)
    {
        ArgumentNullException.ThrowIfNull(copyId);

        return _holds.FirstOrDefault(hold => copyId.Equals(hold.TrappedCopyId));
    }

    /// <summary>
    /// Ends a claim by its borrower checking the trapped copy out. The hold leaves the queue; the
    /// outcome travels in the event, not in a status the queue keeps.
    /// </summary>
    /// <param name="copyId">The trapped copy being checked out.</param>
    /// <returns>Success, or the reason nothing could be fulfilled.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="copyId"/> is null.</exception>
    public Result Fulfill(CopyId copyId)
    {
        ArgumentNullException.ThrowIfNull(copyId);

        var hold = HoldAwaitingPickupOf(copyId);

        if (hold is null)
        {
            return Result.Failure(
                CirculationErrorCodes.NoSuchHold,
                $"No claim in this queue has copy '{copyId}' set aside.");
        }

        _holds.Remove(hold);

        AddDomainEvent(new HoldFulfilled(Id, hold.Id, hold.BorrowerId, copyId));

        return Result.Success();
    }

    /// <summary>
    /// Tells the borrowers whose set-aside copies are about to go back on the shelf, once each.
    /// </summary>
    /// <param name="today">The day the scheduled process is running.</param>
    /// <remarks>
    /// The window is two days wide — the deadline is today or tomorrow — rather than the single
    /// day the design's <em>expiring tomorrow</em> suggests, so a run that misses a day still
    /// warns somebody before their copy goes. <c>ExpiryWarningSent</c> is what keeps the wider
    /// window from becoming a daily nag. Claims already past their deadline are left alone: they
    /// are expired in the same run, and <em>hurry up</em> about a copy that has just gone back is
    /// worse than silence.
    /// </remarks>
    public void WarnOfImminentExpiry(DateOnly today)
    {
        var expiring = _holds
            .Where(hold => hold.Status == HoldStatus.AwaitingPickup
                && !hold.ExpiryWarningSent
                && hold.PickupDeadline >= today
                && hold.PickupDeadline <= today.AddDays(1))
            .ToList();

        foreach (var hold in expiring)
        {
            hold.NoteExpiryWarned();

            AddDomainEvent(new HoldExpiringSoon(
                Id, hold.Id, hold.BorrowerId, hold.TrappedCopyId!, hold.PickupDeadline!.Value));
        }
    }

    /// <summary>
    /// Ends the claims whose borrowers did not come, and offers each released copy to the next
    /// borrower in good standing.
    /// </summary>
    /// <param name="today">The day the scheduled process is running.</param>
    /// <param name="blocked">
    /// Borrowers a debt blocks, as the caller judged from the balance. Skipped, never removed —
    /// the same net the return moment casts.
    /// </param>
    /// <param name="policy">The circulation policy, which decides how long the next borrower gets.</param>
    /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
    /// <remarks>
    /// <para>
    /// The deadline is the last day the copy waits, so a claim expires the day <em>after</em> it.
    /// An expired claim leaves the queue exactly as a cancelled one does — no penalty attaches,
    /// for the reasons the design gives for declining to punish the no-show — and its copy is
    /// re-offered here rather than handed back to the caller, because a copy on the hold shelf
    /// that no live claim owns is a promise to nobody.
    /// </para>
    /// <para>
    /// The deadline is judged through the calendar as it stands, not as it stood: the trapping
    /// already slid it off a closed day, so ordinarily this moves nothing, but a closure declared
    /// afterwards — a strike landing on the stored deadline — must not cost a borrower their
    /// claim for not walking through a locked door.
    /// </para>
    /// <para>
    /// The copy is released before it is re-offered, so the rule that a trapped copy belongs to
    /// exactly one claim holds at every instant in between.
    /// </para>
    /// </remarks>
    public void ExpireUncollectedHolds(
        DateOnly today,
        IReadOnlySet<BorrowerId> blocked,
        CirculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        ArgumentNullException.ThrowIfNull(policy);

        var uncollected = _holds
            .Where(hold => hold.Status == HoldStatus.AwaitingPickup
                && hold.PickupDeadline is { } deadline
                && policy.Calendar.FirstOpenDayOnOrAfter(deadline) < today)
            .ToList();

        foreach (var hold in uncollected)
        {
            var releasedCopy = hold.TrappedCopyId!;

            _holds.Remove(hold);

            AddDomainEvent(new HoldExpired(Id, hold.Id, hold.BorrowerId, releasedCopy));

            TrapOldestQueued(releasedCopy, policy.PickupDeadlineFor(today), blocked);
        }
    }

    /// <summary>
    /// What ending a claim leaves in the caller's hands.
    /// </summary>
    /// <param name="ReleasedCopyId">The copy the claim had set aside, or <see langword="null"/>
    /// when the claim was still queued. A released copy goes back to the queue — offered to the
    /// next borrower in good standing — and the caller runs that offer, because standing is not
    /// this aggregate's to judge.</param>
    public sealed record Cancellation(CopyId? ReleasedCopyId);

    /// <summary>
    /// Ends a borrower's claim because they owe money.
    /// </summary>
    /// <param name="borrowerId">Whose claim to end.</param>
    /// <returns>
    /// The cancellation when this queue held a claim of theirs, or <see langword="null"/> when it
    /// held none. Absence is not a failure here: the caller sweeps every queue a borrower appears
    /// in and cannot know in advance which of them still holds a live claim.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="borrowerId"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The same removal as <see cref="CancelFor"/> and a different announcement, which is the whole
    /// difference: one confirms a choice, the other reports a consequence nobody chose. Merging them
    /// into one event with a reason would make <em>why did I lose my place</em> a question about a
    /// flag, and it is the first question the borrower asks.
    /// </para>
    /// <para>
    /// Not suspended — removed. A borrower who pays an hour later does not get their place back, and
    /// the rule buys an invariant stronger than itself: nobody in a hold queue owes money, checkable
    /// at any instant rather than only when someone reaches the front.
    /// </para>
    /// </remarks>
    public Cancellation? CancelForDebt(BorrowerId borrowerId)
    {
        ArgumentNullException.ThrowIfNull(borrowerId);

        var hold = _holds.Find(held => held.BorrowerId == borrowerId);

        if (hold is null)
        {
            return null;
        }

        _holds.Remove(hold);

        AddDomainEvent(new HoldCancelledForDebt(Id, hold.Id, borrowerId));

        return new Cancellation(hold.TrappedCopyId);
    }

    /// <summary>
    /// Ends a borrower's claim because they changed their mind. The ordinary exit from a queue,
    /// and it must exist: a hold occupies one of the five places the cap counts, and a borrower
    /// who cannot free a place is punished for having reserved at all.
    /// </summary>
    /// <param name="borrowerId">Whose claim to end.</param>
    /// <returns>The cancellation, or the reason there was nothing to cancel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="borrowerId"/> is null.</exception>
    /// <remarks>
    /// The gap behind a removed claim closes by itself — order is the placement instant, and
    /// positions are wherever the live claims stand. No penalty attaches, for the reasons the
    /// design declines to punish the no-show.
    /// </remarks>
    public Result<Cancellation> CancelFor(BorrowerId borrowerId)
    {
        ArgumentNullException.ThrowIfNull(borrowerId);

        var hold = _holds.FirstOrDefault(hold => hold.BorrowerId == borrowerId);

        if (hold is null)
        {
            return Result<Cancellation>.Failure(
                CirculationErrorCodes.NoSuchHold,
                "No hold of this borrower waits on this edition.");
        }

        _holds.Remove(hold);

        AddDomainEvent(new HoldCancelled(Id, hold.Id, borrowerId));

        return Result<Cancellation>.Success(new Cancellation(hold.TrappedCopyId));
    }
}
