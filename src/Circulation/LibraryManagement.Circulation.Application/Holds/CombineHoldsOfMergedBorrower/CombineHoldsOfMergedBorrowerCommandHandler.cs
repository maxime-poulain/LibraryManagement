using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CombineHoldsOfMergedBorrower;

/// <summary>
/// Handles <see cref="CombineHoldsOfMergedBorrowerCommand"/>.
/// </summary>
/// <param name="queues">Every queue in which the absorbed record holds a live claim.</param>
/// <remarks>
/// <para>
/// <strong>The store question is the handler's, the rules are the aggregate's.</strong> Which
/// queues hold the absorbed borrower's claims cannot be answered without the store, so it is asked
/// here, through the borrower-indexed loader the debt cancellation already needed. Which claims
/// survive once a queue is in hand is that queue's own rule — one aggregate, so no domain service
/// stands between them, which is the same convention that gave the queue <em>union</em> one.
/// </para>
/// <para>
/// <strong>It succeeds when the absorbed record holds no claim anywhere.</strong> The ordinary
/// case, and the one a refusal would turn into a message the announcing drain replays forever.
/// Redelivery costs nothing for the same reason: after the first pass the absorbed identifier is
/// on no live claim, so the sweep returns no queue at all.
/// </para>
/// <para>
/// <strong>The cap is not consulted.</strong> Members §10 answers it: two files of claims can make
/// one file past five, and the cap constrains the <em>act</em> rather than the state — the
/// survivor simply places nothing more until enough of it drains.
/// </para>
/// </remarks>
public sealed class CombineHoldsOfMergedBorrowerCommandHandler(IHoldQueueRepository queues)
    : ICommandHandler<CombineHoldsOfMergedBorrowerCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CombineHoldsOfMergedBorrowerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var absorbed = BorrowerId.Create(command.AbsorbedBorrowerId);
        var surviving = BorrowerId.Create(command.SurvivingBorrowerId);

        var affected = await queues.WithHoldsForBorrowerAsync(absorbed, cancellationToken)
            .ConfigureAwait(false);

        foreach (var queue in affected)
        {
            queue.CombineClaimsOf(absorbed, surviving);
        }

        return Result.Success();
    }
}
