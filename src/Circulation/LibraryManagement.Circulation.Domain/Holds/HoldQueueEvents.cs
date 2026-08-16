using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Circulation.Domain.Holds;

// A hold that ends leaves the aggregate, so these events are the only durable record of a claim's
// story — the history projection is fed by exactly this stream, and an event not published when
// it happened cannot be recovered afterwards.

/// <summary>
/// A claim joined a queue.
/// </summary>
/// <param name="EditionId">The edition queued for.</param>
/// <param name="HoldId">The new claim.</param>
/// <param name="BorrowerId">Whose claim it is.</param>
public sealed record HoldPlaced(EditionId EditionId, HoldId HoldId, BorrowerId BorrowerId) : DomainEvent;

/// <summary>
/// A returned copy was set aside for the first claim instead of being shelved, and the pickup
/// countdown started.
/// </summary>
/// <param name="EditionId">The edition.</param>
/// <param name="HoldId">The claim now awaiting pickup.</param>
/// <param name="BorrowerId">Who should come and collect.</param>
/// <param name="CopyId">The copy on the hold shelf.</param>
/// <param name="PickupDeadline">How long it waits. A consequence the borrower did not choose, so
/// the message always goes out.</param>
public sealed record HoldReadyForPickup(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId,
    CopyId CopyId,
    DateOnly PickupDeadline) : DomainEvent;

/// <summary>
/// A claim ended the way claims hope to: its borrower checked the trapped copy out.
/// </summary>
/// <param name="EditionId">The edition.</param>
/// <param name="HoldId">The claim fulfilled, gone from the queue.</param>
/// <param name="BorrowerId">Who collected.</param>
/// <param name="CopyId">The copy they took.</param>
public sealed record HoldFulfilled(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId,
    CopyId CopyId) : DomainEvent;

/// <summary>
/// A set-aside copy is about to go back on the shelf.
/// </summary>
/// <param name="EditionId">The edition.</param>
/// <param name="HoldId">The claim about to lapse.</param>
/// <param name="BorrowerId">Who should come today.</param>
/// <param name="CopyId">The copy still waiting for them.</param>
/// <param name="PickupDeadline">The last day it waits.</param>
/// <remarks>
/// A consequence the borrower did not choose, so the message always goes out — and the reason the
/// design declines to penalise the no-show at all: this reminder is what it does instead.
/// </remarks>
public sealed record HoldExpiringSoon(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId,
    CopyId CopyId,
    DateOnly PickupDeadline) : DomainEvent;

/// <summary>
/// A claim ended because nobody came for the copy.
/// </summary>
/// <param name="EditionId">The edition.</param>
/// <param name="HoldId">The claim that lapsed, gone from the queue.</param>
/// <param name="BorrowerId">Who did not come.</param>
/// <param name="CopyId">The copy, released and offered to whoever is next.</param>
public sealed record HoldExpired(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId,
    CopyId CopyId) : DomainEvent;

/// <summary>
/// A claim ended because its borrower changed their mind.
/// </summary>
/// <param name="EditionId">The edition.</param>
/// <param name="HoldId">The claim cancelled, gone from the queue.</param>
/// <param name="BorrowerId">Who changed their mind.</param>
/// <remarks>
/// Informational, in the notification design's terms — the confirmation of an act the borrower
/// chose — unlike the cancellation a debt forces, which announces a consequence they did not
/// choose and always goes out.
/// </remarks>
public sealed record HoldCancelled(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId) : DomainEvent;

/// <summary>
/// A claim ended because its borrower owes money.
/// </summary>
/// <param name="EditionId">The edition.</param>
/// <param name="HoldId">The claim cancelled, gone from the queue.</param>
/// <param name="BorrowerId">Who owes.</param>
/// <remarks>
/// <para>
/// Consequential rather than informational, in the notification design's terms: the borrower did
/// not choose this, so the message always goes out — unlike <see cref="HoldCancelled"/>, which
/// confirms an act they chose and may be declined.
/// </para>
/// <para>
/// <strong>One per claim, not one per borrower.</strong> The design named this in the plural, and
/// the plural could not survive the aggregate boundary: a borrower's claims live in as many queues
/// as there are editions, an event is raised by the aggregate whose state changed, and there is no
/// aggregate here that spans them. Grouping several of these into one message is Notifications'
/// work, which is where it belongs — the borrower wants one message, and that is a fact about
/// messages rather than about queues.
/// </para>
/// </remarks>
public sealed record HoldCancelledForDebt(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId) : DomainEvent;

/// <summary>
/// A pickup already announced was withdrawn: the set-aside copy left service before the borrower
/// came, and their claim went back to the head of the queue.
/// </summary>
/// <param name="EditionId">The queue.</param>
/// <param name="HoldId">The claim released.</param>
/// <param name="BorrowerId">Who was promised the copy.</param>
/// <param name="CopyId">The copy that is no longer there to be collected.</param>
/// <remarks>
/// Consequential, always sent: the borrower was told a copy waited for them, and silence now
/// would send them to the desk for a copy that is not there — the trip this whole design exists
/// to spare. The claim itself lost nothing but the wait: it keeps its placement instant, so it
/// stands first for the next copy that comes back.
/// </remarks>
public sealed record HoldPickupWithdrawn(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId,
    CopyId CopyId) : DomainEvent;

/// <summary>
/// A claim was cancelled because its edition has no copy left to serve it with — the last one
/// was withdrawn, lost, or shut away, and nothing is expected back.
/// </summary>
/// <param name="EditionId">The queue that can no longer promise anything.</param>
/// <param name="HoldId">The claim ended.</param>
/// <param name="BorrowerId">Who was waiting.</param>
/// <remarks>
/// Consequential, always sent, and one per claim for the aggregate-boundary reason the debt
/// cancellation records. The message it becomes is the one a queue owes the people in it: a claim
/// is a promise of the next available copy, and a promise that can no longer be kept must be
/// withdrawn out loud rather than left to occupy one of the borrower's five places forever.
/// </remarks>
public sealed record HoldCancelledUnfulfillable(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId) : DomainEvent;

/// <summary>
/// A claim now waits in a different queue, because Catalog merged two records into one.
/// </summary>
/// <param name="EditionId">The queue it waits in from now on — the surviving record.</param>
/// <param name="HoldId">The claim. It kept its identity and its place in time, which is the point.</param>
/// <param name="BorrowerId">Who is waiting.</param>
/// <param name="PreviousEditionId">
/// The queue it waited in. A projection keyed on the pair of edition and hold has to retract that
/// row: here the aggregate's own key changed under the claim, which no other moment in this context
/// does.
/// </param>
/// <remarks>
/// Informational rather than consequential: the borrower loses nothing and keeps their placement
/// instant, so their position among the people ahead of them is exactly what it was. Telling them
/// that two catalog records turned out to describe one edition would explain a cataloger's work to
/// somebody waiting for a book.
/// </remarks>
public sealed record HoldMovedToMergedQueue(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId,
    EditionId PreviousEditionId) : DomainEvent;

/// <summary>
/// One of a borrower's two queued claims ended, because the merge revealed they were claims on the
/// same edition.
/// </summary>
/// <param name="EditionId">The merged queue.</param>
/// <param name="HoldId">The claim that ended — the later of the two.</param>
/// <param name="BorrowerId">Whose claim it was.</param>
/// <param name="SurvivingHoldId">
/// Their claim that remains, and the older one. Carried so a projection can stitch the two
/// histories together rather than showing a claim that stops without a successor.
/// </param>
/// <remarks>
/// <para>
/// Its own event rather than <see cref="HoldCancelled"/>, by the argument that already separated
/// <see cref="HoldCancelledForDebt"/>: <em>why did I lose my place</em> is the first question a
/// borrower asks, and it must not become a question about a flag.
/// </para>
/// <para>
/// And the honest answer here is that nothing was lost. The surviving claim is the older of the
/// two, so the borrower waits from the first time they asked — a message about this would report a
/// change in the catalog as though it were a change in their standing.
/// </para>
/// </remarks>
public sealed record HoldCancelledAsDuplicate(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId BorrowerId,
    HoldId SurvivingHoldId) : DomainEvent;

/// <summary>
/// A claim now belongs to a different member record, because Members merged two files for one
/// person.
/// </summary>
/// <param name="EditionId">The queue. The claim did not move; its borrower's identifier did.</param>
/// <param name="HoldId">The claim. It kept its identity and its place in time.</param>
/// <param name="PreviousBorrowerId">The record that stopped naming a person of its own.</param>
/// <param name="NewBorrowerId">The record the claim answers to from now on.</param>
/// <remarks>
/// The counterpart of <see cref="HoldMovedToMergedQueue"/> for the other merge ADR-0017 decides,
/// and the cheaper of the two by construction: there the aggregate's own key changed under the
/// claim, here a field on the claim changes inside the queue it was always in. Informational at
/// most — the person is the same, their place among the people ahead of them is exactly what it
/// was, and a message about it would explain a back-office correction to somebody waiting for a
/// book.
/// </remarks>
public sealed record HoldBorrowerRepointed(
    EditionId EditionId,
    HoldId HoldId,
    BorrowerId PreviousBorrowerId,
    BorrowerId NewBorrowerId) : DomainEvent;
