using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.RenameAuthor;

/// <summary>
/// Handles <see cref="RenameAuthorCommand"/>.
/// </summary>
/// <param name="authors">The store holding the record to rename.</param>
public sealed class RenameAuthorCommandHandler(IAuthorRepository authors)
    : ICommandHandler<RenameAuthorCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RenameAuthorCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authorId = AuthorId.Create(command.AuthorId);
        var author = await authors.GetByIdAsync(authorId, cancellationToken).ConfigureAwait(false);

        // Reported alone, not accumulated with a name error. RegisterWork accumulates because its
        // missing authors are fields of a request that stays viable once they are fixed; here the
        // missing record is the thing being edited, the request is void without it, and a remark
        // about the name would be a correction to a request the employee is about to abandon.
        if (author is null)
        {
            return Result.Failure(
                CatalogErrorCodes.AuthorNotFound,
                $"No author is catalogued under '{authorId}'.");
        }

        return PersonName.Create(command.NewAuthorizedName).Bind(author.Rename);
    }
}
