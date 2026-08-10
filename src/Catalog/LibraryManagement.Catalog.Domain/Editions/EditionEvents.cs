using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Editions;

/// <summary>
/// An edition was cataloged.
/// </summary>
/// <param name="EditionId">The new edition.</param>
/// <param name="WorkId">The work it prints.</param>
/// <param name="Isbn">The ISBN it bears, or <see langword="null"/> when it bears none.</param>
/// <remarks>
/// The ISBN is nullable here because it is nullable on the edition: the search projection indexes
/// it when there is one, and an edition that bears none simply has no form to answer to yet.
/// </remarks>
public sealed record EditionRegistered(EditionId EditionId, WorkId WorkId, Isbn? Isbn) : DomainEvent;

/// <summary>
/// Two records for one edition became one. The absorbed record stops answering; the survivor goes
/// on.
/// </summary>
/// <param name="AbsorbedEditionId">The record that was merged away and is now a pointer.</param>
/// <param name="SurvivingEditionId">The record that goes on answering.</param>
/// <remarks>
/// The event every downstream context has been waiting on
/// (<c>docs/adr/0017-a-merge-is-an-event-and-circulation-pays-for-it.md</c>). It carries the two
/// identifiers and nothing else: what the absorbed record said, and why a cataloger decided it was
/// a duplicate, is no consumer's business, and a payload that offered those would invite one to
/// grow a use for them.
/// </remarks>
public sealed record EditionsMerged(EditionId AbsorbedEditionId, EditionId SurvivingEditionId)
    : DomainEvent;
