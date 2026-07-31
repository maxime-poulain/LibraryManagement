using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Works.RegisterWork;

/// <summary>
/// Catalogues a work.
/// </summary>
/// <param name="WorkId">The identifier the work will keep, supplied by the caller.</param>
/// <param name="PreferredTitle">The title.</param>
/// <param name="AuthorIds">
/// The authors credited. May be empty: anonymous and traditional works have none, and demanding one
/// would force a librarian to invent an author for <em>Le Roman de Renart</em>.
/// </param>
public sealed record RegisterWorkCommand(
    Guid WorkId,
    string PreferredTitle,
    IReadOnlyList<Guid> AuthorIds) : ICommand<Result>;
