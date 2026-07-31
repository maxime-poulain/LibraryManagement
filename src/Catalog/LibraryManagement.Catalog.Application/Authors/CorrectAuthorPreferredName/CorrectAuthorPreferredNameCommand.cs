using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorPreferredName;

/// <summary>
/// Replaces a preferred name that was wrong, keeping nothing of it.
/// </summary>
/// <param name="AuthorId">The author whose preferred name to correct.</param>
/// <param name="CorrectedName">The preferred name as it should have read.</param>
/// <remarks>
/// This repairs the <em>record</em>: a typo, a mistranscription. Nobody's name, so nothing of it is
/// kept — filed as a variant it would become a searchable access point, and the catalog would
/// preserve forever the one thing it was asked to remove. A person actually known by a new name is
/// renamed with <see cref="RenameAuthor.RenameAuthorCommand"/> instead, which keeps the old form
/// findable.
/// </remarks>
public sealed record CorrectAuthorPreferredNameCommand(
    Guid AuthorId,
    string CorrectedName) : ICommand<Result>;
