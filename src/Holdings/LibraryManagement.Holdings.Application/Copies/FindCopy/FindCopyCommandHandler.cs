using LibraryManagement.Holdings.Domain;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.FindCopy;

/// <summary>
/// Handles <see cref="FindCopyCommand"/>.
/// </summary>
/// <param name="copies">The store holding the copy.</param>
public sealed class FindCopyCommandHandler(ICopyRepository copies)
    : ICommandHandler<FindCopyCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        FindCopyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var copyId = CopyId.Create(command.CopyId);
        var copy = await copies.GetByIdAsync(copyId, cancellationToken).ConfigureAwait(false);

        if (copy is null)
        {
            return Result.Failure(
                HoldingsErrorCodes.CopyNotFound,
                $"No copy is held under '{copyId}'.");
        }

        return copy.Find(command.ReferenceOnly);
    }
}
