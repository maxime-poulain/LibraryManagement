using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorLifeYears;

/// <summary>
/// Handles <see cref="CorrectAuthorLifeYearsCommand"/>.
/// </summary>
/// <param name="authors">The store holding the record to correct.</param>
public sealed class CorrectAuthorLifeYearsCommandHandler(IAuthorRepository authors)
    : ICommandHandler<CorrectAuthorLifeYearsCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CorrectAuthorLifeYearsCommand command,
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

        return LifeYears.Create(command.BirthYear, command.DeathYear)
            .Bind(lifeYears =>
            {
                author.CorrectLifeYears(lifeYears);
                return Result.Success();
            });
    }
}
