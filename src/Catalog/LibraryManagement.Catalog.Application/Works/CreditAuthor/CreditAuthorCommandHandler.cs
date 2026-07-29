using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Works.CreditAuthor;

/// <summary>
/// Handles <see cref="CreditAuthorCommand"/>.
/// </summary>
/// <param name="works">The store holding the work to credit.</param>
/// <param name="authors">Consulted to confirm the credited author is catalogued.</param>
/// <remarks>
/// Crediting an author who does not exist is the rule that spans two aggregates, enforced here for
/// the reason given on <see cref="RegisterWork.RegisterWorkCommandHandler"/>: neither aggregate can
/// enforce it alone, and the check reports which record is missing, which a foreign key would not.
/// </remarks>
public sealed class CreditAuthorCommandHandler(
    IWorkRepository works,
    IAuthorRepository authors) : ICommandHandler<CreditAuthorCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CreditAuthorCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var workId = WorkId.Create(command.WorkId);
        var work = await works.GetByIdAsync(workId, cancellationToken).ConfigureAwait(false);

        if (work is null)
        {
            return Result.Failure(
                CatalogErrorCodes.WorkNotFound,
                $"No work is catalogued under '{workId}'.");
        }

        var authorId = AuthorId.Create(command.AuthorId);
        var exists = await authors.ExistsAsync(authorId, cancellationToken).ConfigureAwait(false);

        if (!exists)
        {
            return Result.Failure(
                CatalogErrorCodes.AuthorNotFound,
                $"No author is catalogued under '{authorId}'.");
        }

        return work.CreditAuthor(authorId);
    }
}
