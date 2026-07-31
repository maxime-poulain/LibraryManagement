using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorHeading;

/// <summary>
/// Handles <see cref="CorrectAuthorHeadingCommand"/>.
/// </summary>
/// <param name="authors">The store holding the record to repair.</param>
public sealed class CorrectAuthorHeadingCommandHandler(IAuthorRepository authors)
    : ICommandHandler<CorrectAuthorHeadingCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CorrectAuthorHeadingCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authorId = AuthorId.Create(command.AuthorId);
        var author = await authors.GetByIdAsync(authorId, cancellationToken).ConfigureAwait(false);

        if (author is null)
        {
            return Result.Failure(
                CatalogErrorCodes.AuthorNotFound,
                $"No author is catalogued under '{authorId}'.");
        }

        return NameForm.Create(command.CorrectedName).Bind(author.CorrectHeading);
    }
}
