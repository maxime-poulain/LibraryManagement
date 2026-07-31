using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Editions.RegisterEdition;

/// <summary>
/// Handles <see cref="RegisterEditionCommand"/>.
/// </summary>
/// <param name="editions">The store to add the new edition to.</param>
/// <param name="works">Consulted to confirm the work being printed is cataloged.</param>
/// <remarks>
/// An edition of a work nobody cataloged is the rule that spans two aggregates, enforced here for
/// the reason given on <see cref="Works.RegisterWork.RegisterWorkCommandHandler"/>. The ISBN and
/// the work's existence are independent, so both mistakes are reported at once.
/// </remarks>
public sealed class RegisterEditionCommandHandler(
    IEditionRepository editions,
    IWorkRepository works) : ICommandHandler<RegisterEditionCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RegisterEditionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new ErrorCollection();

        // Null is a fact — the edition bears no ISBN — and only a value that claims to be one is
        // asked to actually be one.
        Isbn? isbn = null;

        if (command.Isbn is not null)
        {
            Isbn.Create(command.Isbn).Switch(created => isbn = created, errors.AddErrors);
        }

        var workId = WorkId.Create(command.WorkId);
        var workExists = await works.ExistsAsync(workId, cancellationToken).ConfigureAwait(false);

        if (!workExists)
        {
            errors.Add(
                CatalogErrorCodes.WorkNotFound,
                $"No work is cataloged under '{workId}'.");
        }

        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        editions.Add(Edition.Register(EditionId.Create(command.EditionId), workId, isbn));

        return Result.Success();
    }
}
