using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.AddAuthorVariantName;

/// <summary>
/// Handles <see cref="AddAuthorVariantNameCommand"/>.
/// </summary>
/// <param name="authors">The store holding the record to add the form to.</param>
public sealed class AddAuthorVariantNameCommandHandler(IAuthorRepository authors)
    : ICommandHandler<AddAuthorVariantNameCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        AddAuthorVariantNameCommand command,
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

        return PersonName.Create(command.VariantName).Bind(author.AddVariantName);
    }
}
