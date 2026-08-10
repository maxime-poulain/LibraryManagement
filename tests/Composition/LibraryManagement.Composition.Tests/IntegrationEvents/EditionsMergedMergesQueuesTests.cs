using LibraryManagement.Catalog.Application.Editions.MergeEditions;
using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Migrations.SqlServer;
using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Extensions;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.Migrations.SqlServer;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.IntegrationEvents;

/// <summary>
/// The expensive half of a merge, through the whole road: two queues in Circulation's store become
/// one because a cataloger joined two records in Catalog's.
/// </summary>
/// <remarks>
/// <para>
/// Two things only real stores can show. The first is that a claim moving between queues survives
/// the save at all — the claim's key is the pair of edition and hold, so a move is a delete and an
/// insert, and the trapped copy carries a unique index that spans queues. The second is that
/// <em>both</em> of this module's subscribers run on one announcement: the loans and the queues are
/// separate units of consistency, and a composition is where that arrangement is visible.
/// </para>
/// <para>
/// Holdings is absent: nothing here accessions a copy, and the loans are written straight to
/// Circulation's store.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EditionsMergedMergesQueuesTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_EditionsMergedMergesQueues_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        _provider = CompositionRoot.Services()
            .AddCatalogModule(options => options.UseCatalogSqlServer(ConnectionString))
            .AddCirculationModule(options => options.UseCirculationSqlServer(ConnectionString))
            .AddSingleton<IMemberBalance, NoChargesYet>()
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalog.Database.EnsureDeletedAsync(Token);
        await catalog.Database.MigrateAsync(Token);

        var circulation = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
        await circulation.Database.MigrateAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private Task<Result> DispatchAsync(ICommand<Result> command)
        => InScopeAsync(services => services
            .GetRequiredService<ICommandDispatcher>()
            .DispatchAsync(command, Token)
            .AsTask());

    private Task<OutboxDrainOutcome> DrainCatalogAsync()
        => _provider.GetRequiredService<OutboxProcessor<CatalogDbContext>>().ProcessAsync(Token);

    private async Task<Guid> AWorkAsync()
    {
        var workId = Guid.CreateVersion7();
        (await DispatchAsync(new RegisterWorkCommand(workId, "La Horde du Contrevent", [])))
            .HasErrors().ShouldBeFalse();

        return workId;
    }

    private async Task<Guid> AnEditionOfAsync(Guid workId)
    {
        var editionId = Guid.CreateVersion7();
        (await DispatchAsync(new RegisterEditionCommand(editionId, workId, null)))
            .HasErrors().ShouldBeFalse();

        return editionId;
    }

    /// <summary>
    /// A queue written straight to Circulation's store, since what is under test is the reaction
    /// rather than the desk acts that filled the queue.
    /// </summary>
    private async Task AQueueAsync(Guid editionId, params (BorrowerId Borrower, int HoursIn)[] claims)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();

        var queue = HoldQueue.For(EditionId.Create(editionId));

        foreach (var (borrower, hoursIn) in claims)
        {
            queue.PlaceHold(HoldId.Generate(), borrower, ThisMorning.AddHours(hoursIn));
        }

        context.Add(queue);
        await context.SaveChangesAsync(Token);
    }

    private async Task ACopySetAsideAsync(Guid editionId, CopyId copyId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
        var queues = new HoldQueueRepository(context);

        var queue = await queues.GetByEditionAsync(EditionId.Create(editionId), Token);
        queue!.TrapOldestQueued(copyId, Today.AddDays(7), new HashSet<BorrowerId>());

        await context.SaveChangesAsync(Token);
    }

    private Task<HoldQueue?> QueueOfAsync(Guid editionId)
        => InScopeAsync(async services => await new HoldQueueRepository(
                services.GetRequiredService<CirculationDbContext>())
            .GetByEditionAsync(EditionId.Create(editionId), Token));

    [Fact]
    public async Task AMergeInCatalog_MakesTwoQueuesIntoOne()
    {
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);

        var first = BorrowerId.Generate();
        var second = BorrowerId.Generate();
        var third = BorrowerId.Generate();
        await AQueueAsync(surviving, (first, 0), (third, 2));
        await AQueueAsync(absorbed, (second, 1));

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        var outcome = await DrainCatalogAsync();

        outcome.Blockage.ShouldBeNull();
        outcome.Delivered.ShouldBeGreaterThan(0);

        (await QueueOfAsync(absorbed))!.Holds.ShouldBeEmpty();

        // Order needed no rule: the union carries every placement instant with its claim.
        (await QueueOfAsync(surviving))!.QueuedBorrowersInOrder()
            .ShouldBe([first, second, third]);
    }

    [Fact]
    public async Task AClaimWithACopySetAside_MovesWithIt()
    {
        // The save this whole change could plausibly have failed on: the trapped copy carries a
        // unique index that is filtered and spans queues, and the claim changes its key.
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);
        var setAside = CopyId.Generate();

        await AQueueAsync(surviving, (BorrowerId.Generate(), 1));
        await AQueueAsync(absorbed, (BorrowerId.Generate(), 0));
        await ACopySetAsideAsync(absorbed, setAside);

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        (await DrainCatalogAsync()).Blockage.ShouldBeNull();

        var merged = await QueueOfAsync(surviving);
        merged!.TrappedCopyIds().ShouldBe([setAside]);
        merged.Holds.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ABorrowerInBothQueues_KeepsTheirEarliestClaim()
    {
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);
        var twice = BorrowerId.Generate();

        await AQueueAsync(surviving, (twice, 0));
        await AQueueAsync(absorbed, (twice, 1));

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        (await DrainCatalogAsync()).Blockage.ShouldBeNull();

        (await QueueOfAsync(surviving))!.Holds.ShouldHaveSingleItem()
            .PlacedOn.ShouldBe(ThisMorning);
    }

    [Fact]
    public async Task OneAnnouncement_ReachesBothOfThisModulesSubscribers()
    {
        // The loans and the queues are separate units of consistency, so the module registers two
        // subscribers for one contract. Only a composition can show that both actually run.
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);

        await AQueueAsync(absorbed, (BorrowerId.Generate(), 0));
        var loanId = await ALoanOfAsync(absorbed);

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        (await DrainCatalogAsync()).Blockage.ShouldBeNull();

        (await QueueOfAsync(surviving))!.Holds.Count.ShouldBe(1);
        (await EditionOfAsync(loanId)).ShouldBe(surviving);
    }

    private async Task<LoanId> ALoanOfAsync(Guid editionId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();

        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            EditionId.Create(editionId),
            BorrowerId.Generate(),
            Today,
            CirculationPolicy.Current);

        context.Add(loan);
        await context.SaveChangesAsync(Token);

        return loan.Id;
    }

    private Task<Guid> EditionOfAsync(LoanId loanId)
        => InScopeAsync(async services =>
        {
            var loan = await services.GetRequiredService<CirculationDbContext>()
                .Set<Loan>()
                .SingleAsync(stored => stored.Id == loanId, Token);

            return loan.EditionId.Value;
        });
}
