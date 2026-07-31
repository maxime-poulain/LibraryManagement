using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.RenameAuthor;

/// <summary>
/// Files a person under a new name, keeping the old one findable as a variant.
/// </summary>
/// <param name="AuthorId">The author to rename.</param>
/// <param name="NewPreferredName">The name to file the person under from now on.</param>
/// <remarks>
/// This records a fact about the <em>person</em> — a marriage, a transition, a pen name adopted or
/// dropped — and the outgoing preferred name stays findable, because a book printed under it still
/// bears it on its title page. A preferred name that was simply <em>wrong</em> is repaired with
/// <see cref="CorrectAuthorPreferredName.CorrectAuthorPreferredNameCommand"/> instead, which keeps
/// nothing: the two commands differ in exactly what they leave behind.
/// </remarks>
public sealed record RenameAuthorCommand(
    Guid AuthorId,
    string NewPreferredName) : ICommand<Result>;
