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
    public async Task AHoldPlacedInAQueueAlreadyOnFile_ReachesTheTable()
    {
        // The second hold on an edition, which is the ordinary case: the queue row itself does not
        // change, and only the new claim has to be written.
        var queue = HoldQueue.For(EditionId.Generate());
        var first = BorrowerId.Generate();
        var second = BorrowerId.Generate();
        queue.PlaceHold(HoldId.Generate(), first, ThisMorning);
        await StoredAsync(queue);

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new HoldQueueRepository(updating).GetByEditionAsync(queue.Id, Token);
            loaded!.PlaceHold(HoldId.Generate(), second, ThisMorning.AddHours(2));
            await updating.SaveChangesAsync(Token);
        }

        var found = await ReadBackAsync(queue.Id);

        found.QueuedBorrowersInOrder().ShouldBe([first, second]);
    }

    [Fact]
    public async Task AClaimThatEnds_LeavesTheTable()
    {
        var queue = HoldQueue.For(EditionId.Generate());
        var borrower = BorrowerId.Generate();
        var holdId = HoldId.Generate();
        queue.PlaceHold(holdId, borrower, ThisMorning);
        await StoredAsync(queue);

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new HoldQueueRepository(updating).GetByEditionAsync(queue.Id, Token);
            loaded!.CancelFor(holdId, borrower);
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

    // --- What the scheduled process reads and writes ---------------------------------------------

    [Fact]
    public async Task AWarningAlreadySent_SurvivesTheRoundTrip()
    {
        // The hold shelf's own memory, without which every run would announce the same imminent
        // expiry again.
        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        queue.TrapOldestQueued(CopyId.Generate(), Today, NobodyBlocked);
        await StoredAsync(queue);

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new HoldQueueRepository(updating).GetByEditionAsync(queue.Id, Token);
            loaded!.WarnOfImminentExpiry(Today);
            await updating.SaveChangesAsync(Token);
        }

        var found = await ReadBackAsync(queue.Id);

        found.Holds.Single().ExpiryWarningSent.ShouldBeTrue();
    }

    [Fact]
    public async Task WithHoldsAwaitingPickupThrough_FindsTheQueuesTheDayConcerns()
    {
        var soon = HoldQueue.For(EditionId.Generate());
        soon.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        soon.TrapOldestQueued(CopyId.Generate(), Today, NobodyBlocked);
        await StoredAsync(soon);

        var later = HoldQueue.For(EditionId.Generate());
        later.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        later.TrapOldestQueued(CopyId.Generate(), Today.AddDays(5), NobodyBlocked);
        await StoredAsync(later);

        await using var reading = sqlServer.NewContext();
        var found = await new HoldQueueRepository(reading)
            .WithHoldsAwaitingPickupThroughAsync(Today, Token);

        var editions = found.Select(queue => queue.Id).ToList();
        editions.ShouldContain(soon.Id);
        editions.ShouldNotContain(later.Id);
    }

    [Fact]
    public async Task WithHoldsAwaitingPickupThrough_IgnoresAQueueWhereNobodysCopyIsSetAside()
    {
        var waiting = HoldQueue.For(EditionId.Generate());
        waiting.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        await StoredAsync(waiting);

        await using var reading = sqlServer.NewContext();
        var found = await new HoldQueueRepository(reading)
            .WithHoldsAwaitingPickupThroughAsync(Today.AddYears(1), Token);

        found.Select(queue => queue.Id).ShouldNotContain(waiting.Id);
    }

    [Fact]
    public async Task AMerge_MovesEveryClaimBetweenTwoQueues()
    {
        // A claim's key is the pair of edition and hold, so moving one is a DELETE and an INSERT in
        // the same save. Nothing about that is provable by the compiler, which is why it is proved
        // here.
        var absorbed = HoldQueue.For(EditionId.Generate());
        var surviving = HoldQueue.For(EditionId.Generate());
        var moving = HoldId.Generate();
        var staying = HoldId.Generate();
        absorbed.PlaceHold(moving, BorrowerId.Generate(), ThisMorning);
        surviving.PlaceHold(staying, BorrowerId.Generate(), ThisMorning.AddHours(1));
        await StoredAsync(absorbed);
        await StoredAsync(surviving);

        await MergedAsync(absorbed.Id, surviving.Id);

        (await ReadBackAsync(absorbed.Id)).Holds.ShouldBeEmpty();
        (await ReadBackAsync(surviving.Id)).Holds
            .Select(hold => hold.Id).ShouldBe([moving, staying], ignoreOrder: true);
    }

    [Fact]
    public async Task AMerge_MovesAClaimWithItsTrappedCopy_PastTheIndexThatSpansQueues()
    {
        // The one save this change could plausibly fail on, and the reason it is worth an
        // integration test of its own. The trapped copy carries a unique index that is filtered and
        // spans queues, so a move is a row leaving one queue and the same copy arriving in another
        // — and if the insert reached the server before the delete, the index would refuse a state
        // the model never holds.
        var absorbed = HoldQueue.For(EditionId.Generate());
        var surviving = HoldQueue.For(EditionId.Generate());
        absorbed.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        surviving.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning.AddHours(1));
        var setAside = CopyId.Generate();
        absorbed.TrapOldestQueued(setAside, Today.AddDays(7), NobodyBlocked);
        await StoredAsync(absorbed);
        await StoredAsync(surviving);

        await MergedAsync(absorbed.Id, surviving.Id);

        var found = await ReadBackAsync(surviving.Id);
        found.TrappedCopyIds().ShouldBe([setAside]);
        found.Holds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AMerge_LeavingABorrowerWithTwoQueuedClaims_KeepsTheEarliest()
    {
        // The service's rule, proved once against a real store: what is written is what the merge
        // decided, and not what a naive union would have left.
        var borrowerId = BorrowerId.Generate();
        var absorbed = HoldQueue.For(EditionId.Generate());
        var surviving = HoldQueue.For(EditionId.Generate());
        var earliest = HoldId.Generate();
        surviving.PlaceHold(earliest, borrowerId, ThisMorning);
        absorbed.PlaceHold(HoldId.Generate(), borrowerId, ThisMorning.AddHours(1));
        await StoredAsync(absorbed);
        await StoredAsync(surviving);

        await MergedAsync(absorbed.Id, surviving.Id);

        (await ReadBackAsync(surviving.Id)).Holds.ShouldHaveSingleItem().Id.ShouldBe(earliest);
    }

    /// <summary>Runs a merge through a context of its own, so the save is what is under test.</summary>
    private async Task MergedAsync(EditionId absorbedId, EditionId survivingId)
    {
        await using var updating = sqlServer.NewContext();
        var queues = new HoldQueueRepository(updating);

        var absorbed = await queues.GetByEditionAsync(absorbedId, Token);
        var surviving = await queues.GetByEditionAsync(survivingId, Token);

        new HoldQueueMergeDomainService().Merge(absorbed!, surviving!).HasErrors().ShouldBeFalse();

        await updating.SaveChangesAsync(Token);
    }

    [Fact]
    public async Task TheHoldsComeBackWithTheirQueue_WhenTheDayFindsIt()
    {
        // The query has to include what the aggregate will read, or the expiry would look at an
        // empty queue and end nothing.
        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning.AddHours(1));
        queue.TrapOldestQueued(CopyId.Generate(), Today.AddDays(-1), NobodyBlocked);
        await StoredAsync(queue);

        await using var reading = sqlServer.NewContext();
        var found = (await new HoldQueueRepository(reading)
                .WithHoldsAwaitingPickupThroughAsync(Today.AddDays(-1), Token))
            .Single(other => other.Id == queue.Id);

        found.Holds.Count.ShouldBe(2);
    }
}
