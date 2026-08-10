using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Holdings.Domain.Copies;

// Holdings publishes facts about objects. It does not know that Circulation exists, and the one
// thing Circulation needs from it is a question — may this copy be lent? — answered synchronously
// through the module's published language, never announced.
//
// Every event here feeds a projection and nothing else, today. That is not a reason to withhold
// them: the stock ledger a librarian consults — how many copies of this edition, where, in what
// state — is a read model fed by exactly this stream, and an event not published when it happened
// cannot be recovered afterwards.

/// <summary>
/// A copy entered the collection.
/// </summary>
/// <param name="CopyId">The new copy.</param>
/// <param name="EditionId">The edition it is a copy of.</param>
/// <param name="Barcode">The label it was accessioned under.</param>
public sealed record CopyAcquired(CopyId CopyId, EditionId EditionId, Barcode Barcode) : DomainEvent;

/// <summary>
/// A copy moved, and its label with it.
/// </summary>
/// <param name="CopyId">The copy that moved.</param>
/// <param name="PreviousShelfmark">Where it stood. It stops being where anyone should look.</param>
/// <param name="NewShelfmark">Where it stands now.</param>
/// <remarks>
/// Both are carried so a projection keyed on the shelfmark can retract the old one — the same shape
/// a corrected preferred name takes in Catalog.
/// </remarks>
public sealed record CopyReshelved(
    CopyId CopyId,
    Shelfmark PreviousShelfmark,
    Shelfmark NewShelfmark) : DomainEvent;

/// <summary>
/// A copy's label was replaced.
/// </summary>
/// <param name="CopyId">The copy. Its identity did not change, which is the point.</param>
/// <param name="PreviousBarcode">The label that is gone. A scan of it must stop finding this copy.</param>
/// <param name="NewBarcode">The label from now on.</param>
public sealed record CopyRelabelled(
    CopyId CopyId,
    Barcode PreviousBarcode,
    Barcode NewBarcode) : DomainEvent;

/// <summary>
/// A copy now names a different edition, because Catalog merged two records into one.
/// </summary>
/// <param name="CopyId">The copy. Nothing about the object changed, which is the point.</param>
/// <param name="PreviousEditionId">
/// The record that stopped answering. A projection keyed on the edition has to retract this one.
/// </param>
/// <param name="NewEditionId">The record it is filed under from now on.</param>
/// <remarks>
/// The one event here that is not this context's own observation: it reports what Holdings did in
/// answer to a fact Catalog announced. Both identifiers travel for the reason
/// <see cref="CopyRelabelled"/> carries both labels — a stock ledger counting copies per edition
/// must subtract before it adds.
/// </remarks>
public sealed record CopyRepointed(
    CopyId CopyId,
    EditionId PreviousEditionId,
    EditionId NewEditionId) : DomainEvent;

/// <summary>
/// A copy's physical state was recorded.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <param name="PreviousCondition">What it was recorded as before.</param>
/// <param name="NewCondition">What it is recorded as now.</param>
public sealed record CopyConditionRecorded(
    CopyId CopyId,
    CopyCondition PreviousCondition,
    CopyCondition NewCondition) : DomainEvent;

/// <summary>
/// A copy left for repair.
/// </summary>
/// <param name="CopyId">The copy.</param>
public sealed record CopySentForRepair(CopyId CopyId) : DomainEvent;

/// <summary>
/// A copy came back from repair.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <param name="Status">
/// What it returned to. Not always <see cref="CopyStatus.InService"/>: a reference-only copy sent
/// for rebinding comes back reference-only, and a consumer that assumed otherwise would show the
/// library's only copy as lendable.
/// </param>
public sealed record CopyReturnedFromRepair(CopyId CopyId, CopyStatus Status) : DomainEvent;

/// <summary>
/// A copy was set aside for consultation on site and will not be lent.
/// </summary>
/// <param name="CopyId">The copy.</param>
public sealed record CopyRestrictedToReference(CopyId CopyId) : DomainEvent;

/// <summary>
/// A copy that was held back rejoined the lending stock.
/// </summary>
/// <param name="CopyId">The copy.</param>
public sealed record CopyReleasedForLending(CopyId CopyId) : DomainEvent;

/// <summary>
/// A copy is unaccounted for.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <remarks>
/// An observation, not a decision — which is why it can be undone by <see cref="CopyFound"/> and
/// why <see cref="CopyWithdrawn"/> cannot.
/// </remarks>
public sealed record CopyDeclaredLost(CopyId CopyId) : DomainEvent;

/// <summary>
/// A copy nobody could account for turned up.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <param name="Status">What it rejoined — the lending stock, or the reference collection.</param>
public sealed record CopyFound(CopyId CopyId, CopyStatus Status) : DomainEvent;

/// <summary>
/// A copy left the collection on purpose.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <remarks>
/// Weeding, and it is terminal. Nothing that follows can put this copy back; a copy that returned
/// would be accessioned afresh, with an identity of its own.
/// </remarks>
public sealed record CopyWithdrawn(CopyId CopyId) : DomainEvent;
