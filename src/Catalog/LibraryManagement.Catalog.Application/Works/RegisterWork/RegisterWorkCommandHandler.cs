using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Works.RegisterWork;

/// <summary>
/// Handles <see cref="RegisterWorkCommand"/>.
/// </summary>
/// <param name="works">The store to add the new work to.</param>
/// <param name="authors">Consulted to confirm every credited author is catalogued.</param>
/// <remarks>
/// Crediting an author who does not exist is a rule that spans two aggregates, so neither of them
/// can enforce it alone and the handler asks the question. It stays a business rule and not a
/// foreign key: the check reports which author is missing, which a constraint violation does not.
/// </remarks>
public sealed class RegisterWorkCommandHandler(
    IWorkRepository works,
    IAuthorRepository authors) : ICommandHandler<RegisterWorkCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RegisterWorkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new ErrorCollection();

        var title = Title.Create(command.Title);
        title.TapError(errors.AddErrors);

        var authorIds = command.AuthorIds.Select(AuthorId.Create).ToArray();

        // One question per author. A work credits a handful of them, so a batch check would trade a
        // clear failure — naming the author that is missing — for a saving nobody would measure.
        foreach (var authorId in authorIds)
        {
            var exists = await authors.ExistsAsync(authorId, cancellationToken).ConfigureAwait(false);

            if (!exists)
            {
                errors.Add(
                    CatalogErrorCodes.AuthorNotFound,
                    $"No author is catalogued under '{authorId}'.");
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        return title.Bind(validTitle => Work
            .Register(WorkId.Create(command.WorkId), validTitle, authorIds)
            .Match(
                work =>
                {
                    works.Add(work);
                    return Result.Success();
                },
                failures => Result.Failure(failures)));
    }
}
