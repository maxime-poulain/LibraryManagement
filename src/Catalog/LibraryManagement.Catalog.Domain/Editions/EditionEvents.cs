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
