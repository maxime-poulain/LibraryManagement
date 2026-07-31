using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorPreferredName;

/// <summary>
/// Handles <see cref="CorrectAuthorPreferredNameCommand"/>.
/// </summary>
/// <param name="authors">The store holding the record to repair.</param>
public sealed class CorrectAuthorPreferredNameCommandHandler(IAuthorRepository authors)
    : ICommandHandler<CorrectAuthorPreferredNameCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CorrectAuthorPreferredNameCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var authorId = AuthorId.Create(command.AuthorId);
        var author = await authors.GetByIdAsync(authorId, cancellationToken).ConfigureAwait(false);

        if (author is null)
        {
            return Result.Failure(
                CatalogErrorCodes.AuthorNotFound,
                $"No author is cataloged under '{authorId}'.");
        }

        return NameForm.Create(command.CorrectedName).Bind(author.CorrectPreferredName);
    }
}
