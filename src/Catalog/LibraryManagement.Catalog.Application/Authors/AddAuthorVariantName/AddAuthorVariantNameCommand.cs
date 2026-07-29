using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.AddAuthorVariantName;

/// <summary>
/// Records another form a person is known by.
/// </summary>
/// <param name="AuthorId">The author the form leads back to.</param>
/// <param name="VariantName">The alternative form.</param>
/// <remarks>
/// A variant is not decoration: it is what lets a search for a name someone no longer uses — or
/// never officially used — still find their work, which is the entire reason authority files record
/// them. The heading itself does not move; a person actually filed under a new name is renamed with
/// <see cref="RenameAuthor.RenameAuthorCommand"/>.
/// </remarks>
public sealed record AddAuthorVariantNameCommand(
    Guid AuthorId,
    string VariantName) : ICommand<Result>;
