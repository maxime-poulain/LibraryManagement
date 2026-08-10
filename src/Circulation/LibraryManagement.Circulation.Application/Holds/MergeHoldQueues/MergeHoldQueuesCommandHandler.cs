using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.MergeHoldQueues;

/// <summary>
/// Handles <see cref="MergeHoldQueuesCommand"/>.
/// </summary>
/// <param name="queues">The two queues, loaded here because the caller can name them.</param>
/// <param name="merge">Where the rules of a union live.</param>
/// <remarks>
/// <para>
/// <strong>The store questions are the handler's, the rules are the service's.</strong> Which claims
/// survive a union is decided about two queues already in hand and needs no store, so it is a domain
/// service; whether either queue exists cannot be answered without one, so it is asked here. That is
/// the boundary the convention draws, and this is its second instance after Catalog's merge.
/// </para>
/// <para>
/// <strong>It succeeds when the absorbed record had no queue.</strong> Most editions have none —
/// a queue exists from its first claim on — and this arrives from another module's drain, where a
/// refusal would make that drain replay a message it handled correctly, forever.
/// </para>
/// <para>
/// <strong>A queue is opened for the survivor when it has none.</strong> The absorbed queue's claims
/// have to land somewhere, and an edition nobody had queued for until now inherits people who were
/// waiting for what turns out to be the same book.
/// </para>
/// <para>
/// <strong>Redelivery costs nothing.</strong> On a replay the absorbed queue is empty, so nothing
/// moves and nothing is announced — the same idempotence by an emptying question that the copies and
/// the loans rely on.
/// </para>
/// </remarks>
public sealed class MergeHoldQueuesCommandHandler(
    IHoldQueueRepository queues,
    IHoldQueueMergeDomainService merge) : ICommandHandler<MergeHoldQueuesCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        MergeHoldQueuesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var absorbedId = EditionId.Create(command.AbsorbedEditionId);
        var survivingId = EditionId.Create(command.SurvivingEditionId);

        var absorbed = await queues.GetByEditionAsync(absorbedId, cancellationToken)
            .ConfigureAwait(false);

        if (absorbed is null || absorbed.Holds.Count == 0)
        {
            return Result.Success();
        }

        var surviving = await queues.GetByEditionAsync(survivingId, cancellationToken)
            .ConfigureAwait(false);

        if (surviving is null)
        {
            surviving = HoldQueue.For(survivingId);
            queues.Add(surviving);
        }

        return merge.Merge(absorbed, surviving);
    }
}
