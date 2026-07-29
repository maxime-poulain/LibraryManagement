using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Editions.RegisterEdition;

/// <summary>
/// Catalogues an edition of a work.
/// </summary>
/// <param name="EditionId">The identifier the edition will keep, supplied by the caller.</param>
/// <param name="WorkId">The work this edition prints. It must already be catalogued.</param>
/// <param name="Isbn">
/// The ISBN the edition bears, in any written form, or <see langword="null"/> when it bears none —
/// grey literature and everything printed before 1970 carry no ISBN, and those are exactly what the
/// manual commands exist to catalogue.
/// </param>
public sealed record RegisterEditionCommand(
    Guid EditionId,
    Guid WorkId,
    string? Isbn) : ICommand<Result>;
