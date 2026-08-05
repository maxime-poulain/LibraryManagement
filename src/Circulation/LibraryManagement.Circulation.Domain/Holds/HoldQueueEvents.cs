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
