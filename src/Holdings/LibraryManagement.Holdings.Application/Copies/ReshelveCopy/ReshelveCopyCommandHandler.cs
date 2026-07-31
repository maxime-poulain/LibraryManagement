using LibraryManagement.Holdings.Domain;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.ReshelveCopy;

/// <summary>
/// Handles <see cref="ReshelveCopyCommand"/>.
/// </summary>
/// <param name="copies">The store holding the copy.</param>
public sealed class ReshelveCopyCommandHandler(ICopyRepository copies)
    : ICommandHandler<ReshelveCopyCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        ReshelveCopyCommand command,
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

        return Shelfmark.Create(command.Shelfmark).Bind(copy.Reshelve);
    }
}
