using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.ReleaseTrappedCopy;

/// <summary>
/// Handles <see cref="ReleaseTrappedCopyCommand"/>.
/// </summary>
/// <param name="queues">The one queue, if any, in which the copy is set aside.</param>
/// <remarks>
/// Success when no claim had the copy set aside, which is the ordinary case by far: most
/// departures from service concern copies nobody was promised, and this arrives from another
/// module's drain — a refusal would make that drain replay a correctly handled message forever.
/// The same shape makes a redelivery safe: the first delivery released the claim, the second
/// finds nothing trapped.
/// </remarks>
public sealed class ReleaseTrappedCopyCommandHandler(
    IHoldQueueRepository queues) : ICommandHandler<ReleaseTrappedCopyCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        ReleaseTrappedCopyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var copyId = CopyId.Create(command.CopyId);

        var queue = await queues.TrappingCopyAsync(copyId, cancellationToken)
            .ConfigureAwait(false);

        queue?.ReleaseTrappedCopy(copyId);

        return Result.Success();
    }
}
