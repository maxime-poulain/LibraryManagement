using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorHeading;

/// <summary>
/// Replaces a heading that was wrong, keeping nothing of it.
/// </summary>
/// <param name="AuthorId">The author whose heading to correct.</param>
/// <param name="CorrectedName">The heading as it should have read.</param>
/// <remarks>
/// This repairs the <em>record</em>: a typo, a mistranscription. Nobody's name, so nothing of it is
/// kept — filed as a variant it would become a searchable access point, and the catalogue would
/// preserve forever the one thing it was asked to remove. A person actually known by a new name is
/// renamed with <see cref="RenameAuthor.RenameAuthorCommand"/> instead, which keeps the old form
/// findable.
/// </remarks>
public sealed record CorrectAuthorHeadingCommand(
    Guid AuthorId,
    string CorrectedName) : ICommand<Result>;
