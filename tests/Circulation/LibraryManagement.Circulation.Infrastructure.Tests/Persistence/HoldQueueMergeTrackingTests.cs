using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Infrastructure.Tests.Persistence;

/// <summary>
/// What the change tracker makes of a merge, proved without a database.
/// </summary>
/// <remarks>
/// <para>
/// <strong>No <c>Category=Integration</c>, deliberately.</strong> Nothing here opens a connection:
/// the context is built against a server that does not exist, the queues are attached rather than
/// loaded, and <c>DetectChanges</c> is called directly. The rule under test is EF's, not SQL
/// Server's, so the fast gate is where it belongs — and this is the failure that took a Docker run
/// to find once.
/// </para>
/// <para>
/// The rule: <strong>an owned entity cannot change owners.</strong> A hold's key is the pair of its
/// queue's edition and its own identifier, which makes the foreign key identifying and the child's
/// identity contain its parent's. Handing the same object to another queue is refused outright, so
/// a merge hands over copies and the row moves as a deletion and an insertion.
/// </para>
/// </remarks>
public sealed class HoldQueueMergeTrackingTests
{
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    // A context that will never be asked to connect. Building the model needs a provider; using it
    // this way needs no server, which is the whole point.
    private static CirculationDbContext ADetachedContext()
    {
        var options = new DbContextOptionsBuilder<CirculationDbContext>()
            .UseCirculationSqlServer("Server=(nowhere);Database=none;Trusted_Connection=True")
            .Options;

        return new CirculationDbContext(options);
    }

    [Fact]
    public void AMerge_LeavesTheTrackerWithADeletionAndAnInsertion()
    {
        using var context = ADetachedContext();

        var absorbed = HoldQueue.For(EditionId.Generate());
        var surviving = HoldQueue.For(EditionId.Generate());
        var holdId = HoldId.Generate();
        absorbed.PlaceHold(holdId, BorrowerId.Generate(), ThisMorning);
        surviving.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning.AddHours(1));

        // Attached, not added: this is the state a repository hands the handler — two aggregates
        // the store already holds.
        context.Attach(absorbed);
        context.Attach(surviving);

        new HoldQueueMergeDomainService().Merge(absorbed, surviving).HasErrors().ShouldBeFalse();

        // The call the save makes first, and where moving an owned entity throws.
        Should.NotThrow(context.ChangeTracker.DetectChanges);

        var holds = context.ChangeTracker.Entries<Hold>().ToList();

        holds.Count(entry => entry.State == EntityState.Deleted).ShouldBe(1);
        holds.Count(entry => entry.State == EntityState.Added).ShouldBe(1);

        // And the claim is the same claim by the only measure the domain has: its identifier
        // travelled with it.
        holds.Single(entry => entry.State == EntityState.Added).Entity.Id.ShouldBe(holdId);
        holds.Single(entry => entry.State == EntityState.Deleted).Entity.Id.ShouldBe(holdId);
    }

    [Fact]
    public void AMerge_MovingAClaimWithACopySetAside_IsTrackedTheSameWay()
    {
        // The case that carries the filtered unique index across queues. Whether SQL Server accepts
        // the pair of statements is the integration suite's question; that the tracker produces
        // them at all is this one's.
        using var context = ADetachedContext();

        var absorbed = HoldQueue.For(EditionId.Generate());
        var surviving = HoldQueue.For(EditionId.Generate());
        absorbed.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);
        surviving.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning.AddHours(1));
        var setAside = CopyId.Generate();
        absorbed.TrapOldestQueued(setAside, new DateOnly(2026, 3, 21), new HashSet<BorrowerId>());

        context.Attach(absorbed);
        context.Attach(surviving);

        new HoldQueueMergeDomainService().Merge(absorbed, surviving);

        Should.NotThrow(context.ChangeTracker.DetectChanges);

        context.ChangeTracker.Entries<Hold>()
            .Single(entry => entry.State == EntityState.Added)
            .Entity.TrappedCopyId.ShouldBe(setAside);
    }
}
