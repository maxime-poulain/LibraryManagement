using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Infrastructure.Persistence;

/// <summary>
/// Implements <see cref="IHoldQueueRepository"/> over <see cref="CirculationDbContext"/>.
/// </summary>
/// <param name="context">The module's store.</param>
public sealed class HoldQueueRepository(CirculationDbContext context) : IHoldQueueRepository
{
    /// <inheritdoc/>
    public async ValueTask<HoldQueue?> GetByEditionAsync(
        EditionId editionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(editionId);

        // The holds come with the queue. They are the aggregate, not a detail to opt into: every
        // decision the queue makes reads them.
        return await context.HoldQueues
            .Include(queue => queue.Holds)
            .FirstOrDefaultAsync(queue => queue.Id == editionId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> CountLiveForBorrowerAsync(
        BorrowerId borrowerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(borrowerId);

        // Across every queue, both statuses: a trapped copy on the hold shelf occupies one of the
        // five places exactly as a borrowed one does.
        return await context.HoldQueues
            .SelectMany(queue => queue.Holds)
            .CountAsync(hold => hold.BorrowerId == borrowerId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Add(HoldQueue queue)
    {
        ArgumentNullException.ThrowIfNull(queue);

        // Tracked, not written. The module's unit of work writes once the command has succeeded.
        context.HoldQueues.Add(queue);
    }
}
