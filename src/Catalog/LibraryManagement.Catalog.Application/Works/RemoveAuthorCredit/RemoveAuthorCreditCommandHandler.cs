using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Works.RemoveAuthorCredit;

/// <summary>
/// Handles <see cref="RemoveAuthorCreditCommand"/>.
/// </summary>
/// <param name="works">The store holding the work to remove the credit from.</param>
public sealed class RemoveAuthorCreditCommandHandler(IWorkRepository works)
    : ICommandHandler<RemoveAuthorCreditCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RemoveAuthorCreditCommand command,
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

        // The domain answers with a fact — removed, or never credited — and neither is a refusal:
        // the command asks for a state, this author not credited on this work, and reaching a state
        // that already held is success. Inventing a failure here would be a rule the domain
        // declined to state. The author record itself is not consulted either: whether it exists
        // cannot change what removing an identifier from a list does.
        work.RemoveAuthorCredit(AuthorId.Create(command.AuthorId));

        return Result.Success();
    }
}
