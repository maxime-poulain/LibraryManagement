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
    /// <para>
    /// This aggregate refuses the one condition it can see: a borrower already holding a live claim
    /// here. The other refusals of the design — a copy on the shelf, an edition already borrowed,
    /// a debt, the cap — span other aggregates and other contexts, and the handler asks them.
    /// </para>
    /// <para>
    /// <strong>This refusal is stricter than the invariant, deliberately.</strong> What the queue
    /// guarantees is <em>at most one queued claim per borrower</em>; a merge may legitimately leave
    /// somebody with a queued claim and a copy already set aside for them (§10 of the tactical
    /// design, and <see cref="IHoldQueueMergeDomainService"/>). Nobody should reach that state by
    /// asking for it at a desk — a borrower with a copy waiting on the hold shelf who queues for the
    /// same edition again has made a mistake, not a request. A desk act may refuse more than the
    /// invariant demands; it may never allow less.
    /// </para>
    /// </remarks>
    public Result PlaceHold(HoldId holdId, BorrowerId borrowerId, DateTimeOffset placedOn)
    {
        ArgumentNullException.ThrowIfNull(holdId);
        ArgumentNullException.ThrowIfNull(borrowerId);

        if (_holds.Any(hold => hold.BorrowerId == borrowerId))
        {
            return Result.Failure(
                CirculationErrorCodes.HoldAlreadyPlaced,
                "This borrower already has a live claim in this queue; one place each.");
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
    /// Takes back the promise a copy carried: the claim it was set aside for returns to the
    /// queue, at the place its own age gives it.
    /// </summary>
    /// <param name="copyId">The copy that is no longer there to be collected.</param>
    /// <returns>The claim released, or <see langword="null"/> when no claim had the copy set
    /// aside — the ordinary case by far, since most departures from service concern copies nobody
    /// was promised.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="copyId"/> is null.</exception>
    /// <remarks>
    /// The claim keeps its <c>PlacedOn</c>, so it stands first among the queued by construction —
    /// the borrower did nothing and loses nothing but the trip. What they lose all the same is
    /// told: the withdrawal of a pickup already announced is a consequence they did not choose,
    /// and silence would send them to the desk for a copy that is not there.
    /// </remarks>
    public Hold? ReleaseTrappedCopy(CopyId copyId)
    {
        ArgumentNullException.ThrowIfNull(copyId);

        var promised = _holds.FirstOrDefault(hold => copyId.Equals(hold.TrappedCopyId));

        if (promised is null)
        {
            return null;
        }

        promised.Release();

        AddDomainEvent(new HoldPickupWithdrawn(Id, promised.Id, promised.BorrowerId, copyId));

        return promised;
    }

    /// <summary>
    /// Ends every claim in the queue because no copy is left to serve any of them.
    /// </summary>
    /// <remarks>
    /// The queue's own end of the promise: a claim is a claim on the next available copy, and an
    /// edition whose last copy left the collection has no <em>next</em> to promise. Cancelled
    /// rather than kept waiting for an acquisition nobody has decided on — a claim that cannot be
    /// served occupies one of the borrower's five places for as long as it lives, and living on
    /// hope is the queue quietly degrading the cap. Each ending is announced per claim, exactly
    /// as the debt cancellation is, and for the same reason: the borrower's first question is
    /// about their own claim, not about the queue.
    /// </remarks>
    public void CancelAllUnfulfillable()
    {
        foreach (var hold in _holds.ToList())
        {
            _holds.Remove(hold);

            AddDomainEvent(new HoldCancelledUnfulfillable(Id, hold.Id, hold.BorrowerId));
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
    /// <param name="borrowerId">Whose claims to end.</param>
    /// <returns>
    /// One cancellation per claim of theirs this queue held, empty when it held none. Absence is
    /// not a failure here: the caller sweeps every queue a borrower appears in and cannot know in
    /// advance which of them still holds a live claim.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="borrowerId"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Every claim of theirs, and the plural is load-bearing.</strong> A merged queue may
    /// hold two claims of one borrower — one queued and one awaiting pickup — so ending the first
    /// found would leave the other behind, and with it a person who owes money still occupying a
    /// place. That invariant is exactly what this rule buys: <em>nobody in a hold queue owes
    /// money</em>, checkable at any instant.
    /// </para>
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
    public IReadOnlyList<Cancellation> CancelForDebt(BorrowerId borrowerId)
    {
        ArgumentNullException.ThrowIfNull(borrowerId);

        var theirs = _holds.Where(held => held.BorrowerId == borrowerId).ToList();

        var cancellations = new List<Cancellation>(theirs.Count);

        foreach (var hold in theirs)
        {
            _holds.Remove(hold);

            AddDomainEvent(new HoldCancelledForDebt(Id, hold.Id, borrowerId));

            cancellations.Add(new Cancellation(hold.TrappedCopyId));
        }

        return cancellations;
    }

    /// <summary>
    /// Ends a borrower's claim because they changed their mind. The ordinary exit from a queue,
    /// and it must exist: a hold occupies one of the five places the cap counts, and a borrower
    /// who cannot free a place is punished for having reserved at all.
    /// </summary>
    /// <param name="holdId">Which claim to end.</param>
    /// <param name="borrowerId">Whose claim it must be.</param>
    /// <returns>The cancellation, or the reason there was nothing to cancel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>The claim is named, and the aggregate refuses rather than guesses.</strong> This once
    /// took the borrower alone and found their claim with a first match, which was correct exactly
    /// as long as a borrower could appear only once in a queue. A merge ends that: given two claims
    /// it would remove an arbitrary one, announce the cancellation and return success — the desk
    /// tells the borrower it is done and the borrower is still in the queue, with nothing thrown and
    /// nothing logged. The identifier crossing the desk is the price of that silence being
    /// impossible.
    /// </para>
    /// <para>
    /// The borrower is still required, and checked. It is not redundant: a hold identifier alone
    /// would let a desk end somebody else's claim by naming it, and the desk act is <em>cancel my
    /// hold</em>.
    /// </para>
    /// <para>
    /// The gap behind a removed claim closes by itself — order is the placement instant, and
    /// positions are wherever the live claims stand. No penalty attaches, for the reasons the
    /// design declines to punish the no-show.
    /// </para>
    /// </remarks>
    public Result<Cancellation> CancelFor(HoldId holdId, BorrowerId borrowerId)
    {
        ArgumentNullException.ThrowIfNull(holdId);
        ArgumentNullException.ThrowIfNull(borrowerId);

        var hold = _holds.FirstOrDefault(held => held.Id == holdId);

        if (hold is null || hold.BorrowerId != borrowerId)
        {
            // One refusal for both, deliberately: naming a claim that is not yours and naming one
            // that does not exist must be indistinguishable from outside, or the refusal becomes a
            // way to ask whether a given hold identifier belongs to somebody.
            return Result<Cancellation>.Failure(
                CirculationErrorCodes.NoSuchHold,
                "No hold of this borrower waits on this edition under that identifier.");
        }

        _holds.Remove(hold);

        AddDomainEvent(new HoldCancelled(Id, hold.Id, borrowerId));

        return Result<Cancellation>.Success(new Cancellation(hold.TrappedCopyId));
    }

    /// <summary>
    /// Takes every live claim of another queue into this one.
    /// </summary>
    /// <param name="absorbed">The queue whose edition stopped being a record of its own.</param>
    /// <remarks>
    /// <para>
    /// <strong>Deliberately unguarded, and deliberately <c>internal</c>.</strong> Whether two
    /// queues may be joined at all is <see cref="IHoldQueueMergeDomainService"/>'s decision — the
    /// rule concerns the pair, and belongs to neither queue alone. This method is what remains once
    /// the decision is made; the collisions the union creates are read afterwards by
    /// <see cref="KeepEarliestQueuedClaimOf"/>, one borrower at a time.
    /// </para>
    /// <para>
    /// Order needs no rule and gets none: a queue is served by placement instant, and the union
    /// carries every instant with its claim. Two queues interleave correctly by construction, which
    /// is the one part of a merge that costs nothing.
    /// </para>
    /// </remarks>
    internal void AbsorbHoldsFrom(HoldQueue absorbed)
    {
        ArgumentNullException.ThrowIfNull(absorbed);

        foreach (var hold in absorbed.Surrender())
        {
            _holds.Add(hold);

            AddDomainEvent(new HoldMovedToMergedQueue(Id, hold.Id, hold.BorrowerId, absorbed.Id));
        }
    }

    /// <summary>
    /// Makes one borrower's claims out of two borrowers' claims, because Members merged two files
    /// for one person.
    /// </summary>
    /// <param name="absorbedBorrowerId">The record that stopped naming a person of its own.</param>
    /// <param name="survivingBorrowerId">The record the claims answer to from now on.</param>
    /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Public where the queue-merge mutators are internal, and the difference is the
    /// convention's own boundary.</strong> Which claims survive a union of two <em>queues</em> is a
    /// rule about a pair, so it lives in <see cref="IHoldQueueMergeDomainService"/> and this
    /// aggregate only executes it. Which of one borrower's claims survive in <em>this</em> queue is
    /// a rule about this aggregate alone, and a service holding it would be a service holding one
    /// aggregate's invariant — the case the convention exists to refuse. The two merges apply the
    /// same survival rules; they enter through doors that match what each one spans.
    /// </para>
    /// <para>
    /// Nothing moves and nothing interleaves — the cheap half of what a merged member costs this
    /// context, as the queue union was the expensive half of a merged edition. Each claim changes
    /// whose it says it is, in place; then the borrower who may now hold two is read against the
    /// invariant exactly as a merged queue is.
    /// </para>
    /// <para>
    /// Doing nothing for a pair already combined — or for a queue the absorbed borrower has no
    /// claim in — is what lets a redelivered announcement cost nothing.
    /// </para>
    /// </remarks>
    public void CombineClaimsOf(BorrowerId absorbedBorrowerId, BorrowerId survivingBorrowerId)
    {
        ArgumentNullException.ThrowIfNull(absorbedBorrowerId);
        ArgumentNullException.ThrowIfNull(survivingBorrowerId);

        if (absorbedBorrowerId == survivingBorrowerId)
        {
            return;
        }

        var theirs = _holds.Where(hold => hold.BorrowerId == absorbedBorrowerId).ToList();

        if (theirs.Count == 0)
        {
            return;
        }

        foreach (var hold in theirs)
        {
            hold.RepointTo(survivingBorrowerId);

            AddDomainEvent(
                new HoldBorrowerRepointed(Id, hold.Id, absorbedBorrowerId, survivingBorrowerId));
        }

        KeepEarliestQueuedClaimOf(survivingBorrowerId);
    }

    /// <summary>
    /// Keeps the earliest of a borrower's queued claims and ends the rest as duplicates naming it.
    /// </summary>
    /// <param name="borrowerId">The borrower who may hold more than the invariant permits.</param>
    /// <remarks>
    /// <para>
    /// The queued half of the survival rules, and the enforcement of the invariant as restated:
    /// <em>at most one queued claim per borrower</em>. A claim awaiting pickup is never touched —
    /// a copy is physically on the hold shelf with somebody's name on it, and dropping the claim
    /// would strand the copy and break a promise already made in the world.
    /// </para>
    /// <para>
    /// It lived in <see cref="IHoldQueueMergeDomainService"/> until the member merge asked the same
    /// question of one queue alone, which is what showed the rule had always been about this
    /// aggregate's own claims rather than about the pair of queues. <c>internal</c> so the service
    /// still reaches it; <see cref="CombineClaimsOf"/> is the other caller.
    /// </para>
    /// </remarks>
    internal void KeepEarliestQueuedClaimOf(BorrowerId borrowerId)
    {
        ArgumentNullException.ThrowIfNull(borrowerId);

        var queued = _holds
            .Where(hold => hold.Status == HoldStatus.Queued && hold.BorrowerId == borrowerId)
            .OrderBy(hold => hold.PlacedOn)
            .ToList();

        if (queued.Count < 2)
        {
            return;
        }

        var earliest = queued[0];

        foreach (var superseded in queued.Skip(1))
        {
            CancelAsDuplicate(superseded, earliest.Id);
        }
    }

    /// <summary>
    /// Ends a claim a merge made redundant, and says which of the borrower's claims remains.
    /// </summary>
    /// <param name="hold">The claim to end.</param>
    /// <param name="survivingHoldId">Their claim that stays — the older of the two.</param>
    /// <remarks>
    /// The removal that follows <see cref="KeepEarliestQueuedClaimOf"/>'s judgement, and private
    /// because that judgement is now the only caller: it was <c>internal</c> for the queue-merge
    /// service while the service chose which claim was redundant, and the choosing moved in here
    /// with the rule.
    /// </remarks>
    private void CancelAsDuplicate(Hold hold, HoldId survivingHoldId)
    {
        _holds.Remove(hold);

        AddDomainEvent(
            new HoldCancelledAsDuplicate(Id, hold.Id, hold.BorrowerId, survivingHoldId));
    }

    /// <summary>Empties this queue, handing copies of its live claims to the caller.</summary>
    /// <remarks>
    /// <para>
    /// No event: the claims are not ending, they are moving, and the queue they arrive in announces
    /// that. An announcement here would report the same fact twice from two aggregates, and a
    /// consumer would have to work out that they are one.
    /// </para>
    /// <para>
    /// Copies rather than the objects themselves, because a hold is owned by its queue and an owned
    /// entity cannot change owners — see <see cref="Hold.SameClaimInAnotherQueue"/>. Every field
    /// travels, the identifier included, so it is the same claim by every measure the domain has.
    /// </para>
    /// </remarks>
    private List<Hold> Surrender()
    {
        var surrendered = _holds.ConvertAll(hold => hold.SameClaimInAnotherQueue());

        _holds.Clear();

        return surrendered;
    }

}
