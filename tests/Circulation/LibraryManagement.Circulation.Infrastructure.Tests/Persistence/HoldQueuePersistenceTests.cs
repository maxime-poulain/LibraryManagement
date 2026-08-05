using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class HoldQueuePersistenceTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlySet<BorrowerId> NobodyBlocked = new HashSet<BorrowerId>();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<HoldQueue> StoredAsync(HoldQueue queue)
    {
        await using var writing = sqlServer.NewContext();
        writing.Add(queue);
        await writing.SaveChangesAsync(Token);
        return queue;
    }

    private async Task<HoldQueue> ReadBackAsync(EditionId editionId)
    {
        await using var reading = sqlServer.NewContext();
        return await new HoldQueueRepository(reading).GetByEditionAsync(editionId, Token)
            ?? throw new InvalidOperationException("The queue did not come back.");
    }

    [Fact]
    public async Task AQueue_ComesBackWithItsClaimsAndTheirOrder()
    {
        var queue = HoldQueue.For(EditionId.Generate());
        var first = BorrowerId.Generate();
        var second = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), second, ThisMorning.AddHours(2));
        queue.PlaceHold(HoldId.Generate(), first, ThisMorning);
        await StoredAsync(queue);

        var found = await ReadBackAsync(queue.Id);

        found.Holds.Count.ShouldBe(2);
        found.QueuedBorrowersInOrder().ShouldBe([first, second]);
    }

    [Fact]
    public async Task ATrappedClaim_ComesBackTrapped()
    {
        // The one piece of hold state that exists to keep a promise, so the one worth proving the
        // store actually keeps.
        var queue = HoldQueue.For(EditionId.Generate());
        var borrower = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), borrower, ThisMorning);
        var copyId = CopyId.Generate();
        queue.TrapOldestQueued(copyId, Today.AddDays(7), NobodyBlocked);
        await StoredAsync(queue);

        var found = await ReadBackAsync(queue.Id);

        var hold = found.Holds.Single();
        hold.Status.ShouldBe(HoldStatus.AwaitingPickup);
        hold.TrappedCopyId.ShouldBe(copyId);
        hold.PickupDeadline.ShouldBe(Today.AddDays(7));
    }

    [Fact]
    public async Task AClaimThatEnds_LeavesTheTable()
    {
        var queue = HoldQueue.For(EditionId.Generate());
        var borrower = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), borrower, ThisMorning);
        await StoredAsync(queue);

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new HoldQueueRepository(updating).GetByEditionAsync(queue.Id, Token);
            loaded!.CancelFor(borrower);
            await updating.SaveChangesAsync(Token);
        }

        var found = await ReadBackAsync(queue.Id);

        found.Holds.ShouldBeEmpty();
    }

    [Fact]
    public async Task OneCopyPromisedInTwoQueues_IsRefusedByTheStore()
    {
        // The aggregate holds "one hold per trapped copy" within a queue; this filtered index
        // holds it across them, where no aggregate can see.
        var copyId = CopyId.Generate();

        var one = HoldQueue.For(EditionId.Generate());
        one.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        one.TrapOldestQueued(copyId, Today.AddDays(7), NobodyBlocked);
        await StoredAsync(one);

        var other = HoldQueue.For(EditionId.Generate());
        other.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        other.TrapOldestQueued(copyId, Today.AddDays(7), NobodyBlocked);

        await using var writing = sqlServer.NewContext();
        writing.Add(other);

        await Should.ThrowAsync<DbUpdateException>(() => writing.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task CountingABorrowersLiveHolds_ReachesAcrossQueues()
    {
        var borrower = BorrowerId.Generate();

        var one = HoldQueue.For(EditionId.Generate());
        one.PlaceHold(HoldId.Generate(), borrower, ThisMorning);
        await StoredAsync(one);

        var other = HoldQueue.For(EditionId.Generate());
        other.PlaceHold(HoldId.Generate(), borrower, ThisMorning);
        other.TrapOldestQueued(CopyId.Generate(), Today.AddDays(7), NobodyBlocked);
        await StoredAsync(other);

        await using var reading = sqlServer.NewContext();
        var count = await new HoldQueueRepository(reading)
            .CountLiveForBorrowerAsync(borrower, Token);

        // Queued and awaiting pickup alike: a trapped copy occupies a place exactly as a borrowed
        // one does.
        count.ShouldBe(2);
    }
}
