using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Works.RetitleWork;

/// <summary>
/// Handles <see cref="RetitleWorkCommand"/>.
/// </summary>
/// <param name="works">The store holding the work to retitle.</param>
public sealed class RetitleWorkCommandHandler(IWorkRepository works)
    : ICommandHandler<RetitleWorkCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RetitleWorkCommand command,
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

        return Title.Create(command.NewPreferredTitle)
            .Bind(title =>
            {
                work.Retitle(title);
                return Result.Success();
            });
    }
}
