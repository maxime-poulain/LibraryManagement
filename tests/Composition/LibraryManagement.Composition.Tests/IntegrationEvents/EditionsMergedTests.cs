using LibraryManagement.Catalog.Application.Editions.MergeEditions;
using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Migrations.SqlServer;
using LibraryManagement.Holdings.Application.Copies.AcquireCopy;
using LibraryManagement.Holdings.Application.Copies.WithdrawCopy;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Extensions;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Holdings.Migrations.SqlServer;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.IntegrationEvents;

/// <summary>
/// The first thing Catalog says on its own initiative, travelling the whole road to the module that
/// held the identifier: an aggregate in Catalog, an outbox row in Catalog's schema, a drain, a
/// translation into a flat contract, a subscriber in Holdings, and copies rewritten in Holdings' own
/// transaction.
/// </summary>
/// <remarks>
/// <para>
/// The second passage in the solution and the first in this direction. Until now Catalog was asked
/// questions and answered them; here it states a fact and does not learn what anyone did with it.
/// </para>
/// <para>
/// A unit test of the subscriber cannot reach what this proves. The subscriber runs inside
/// <em>Catalog's</em> drain, whose save is on Catalog's context: one that wrote to
/// <c>HoldingsDbContext</c> directly would leave the change tracked where nobody saves, persisted
/// nowhere, reported by nothing — and every unit test of it would still pass.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EditionsMergedTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    private int _labels;

    // This class's own database: one that drops and rebuilds schemas cannot share with the
    // collection's other classes.
    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_EditionsMerged_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        _provider = CompositionRoot.Services()
            .AddCatalogModule(options => options.UseCatalogSqlServer(ConnectionString))
            .AddHoldingsModule(options => options.UseHoldingsSqlServer(ConnectionString))
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalog.Database.EnsureDeletedAsync(Token);
        await catalog.Database.MigrateAsync(Token);

        var holdings = scope.ServiceProvider.GetRequiredService<HoldingsDbContext>();
        await holdings.Database.MigrateAsync(Token);
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

    /// <summary>One work, so that two editions of it may legitimately be merged.</summary>
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

    private async Task<Guid> ACopyOfAsync(Guid editionId)
    {
        var copyId = Guid.CreateVersion7();
        (await DispatchAsync(new AcquireCopyCommand(
                copyId, editionId, $"3123456789{_labels++:D4}", "843.912 SAI", Today)))
            .HasErrors().ShouldBeFalse();

        return copyId;
    }

    private Task<Guid> EditionOfAsync(Guid copyId)
        => InScopeAsync(async services =>
        {
            var copy = await services.GetRequiredService<HoldingsDbContext>()
                .Set<Copy>()
                .SingleAsync(stored => stored.Id == CopyId.Create(copyId), Token);

            return copy.EditionId.Value;
        });

    [Fact]
    public async Task AMergeInCatalog_RefilesTheCopiesInHoldings()
    {
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);

        var first = await ACopyOfAsync(absorbed);
        var second = await ACopyOfAsync(absorbed);
        var bystander = await ACopyOfAsync(await AnEditionOfAsync(workId));

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        (await EditionOfAsync(first)).ShouldBe(absorbed, "nothing has been drained yet");

        var outcome = await DrainCatalogAsync();

        outcome.Blockage.ShouldBeNull();
        outcome.Delivered.ShouldBeGreaterThan(0);

        // Holdings decided this for itself, in its own transaction, from a fact that told it nothing
        // about copies.
        (await EditionOfAsync(first)).ShouldBe(surviving);
        (await EditionOfAsync(second)).ShouldBe(surviving);

        // And a merge moves what the merge names, and nothing else.
        (await EditionOfAsync(bystander)).ShouldNotBe(surviving);
    }

    [Fact]
    public async Task ACopyThatLeftTheCollection_IsRefiledLikeTheOthers()
    {
        // The case a status check would have quietly broken. A weeded copy still records which
        // edition it was a copy of, and leaving it on the absorbed identifier is exactly the orphan
        // the announcement exists to repair — the sweep is deliberately blind to the status.
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);

        var weeded = await ACopyOfAsync(absorbed);
        (await DispatchAsync(new WithdrawCopyCommand(weeded))).HasErrors().ShouldBeFalse();

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        var outcome = await DrainCatalogAsync();

        outcome.Blockage.ShouldBeNull();
        (await EditionOfAsync(weeded)).ShouldBe(surviving);
    }

    [Fact]
    public async Task TheSameFactDeliveredTwice_LeavesTheCopiesWhereTheFirstDeliveryPutThem()
    {
        // Delivery beyond a module is at-least-once, and this subscriber survives it without a
        // deduplication table: the command sweeps by the absorbed identifier, which after the first
        // pass is on no copy at all.
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);
        var copyId = await ACopyOfAsync(absorbed);

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        await DrainCatalogAsync();
        await ReplayEveryDeliveredMessageAsync();

        var outcome = await DrainCatalogAsync();

        outcome.Blockage.ShouldBeNull();
        (await EditionOfAsync(copyId)).ShouldBe(surviving);
    }

    [Fact]
    public async Task AMergeOfAnEditionNobodyHoldsACopyOf_IsDeliveredAllTheSame()
    {
        // The ordinary case — a cataloger merges records, not shelves — and the one a refusal would
        // turn into a message the drain replays forever.
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        var outcome = await DrainCatalogAsync();

        outcome.Blockage.ShouldBeNull();
        outcome.Delivered.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Clears the processed marks, so the next drain redelivers everything the last one delivered —
    /// the crash between two transactions this mechanism promises to survive, made deterministic.
    /// </summary>
    private async Task ReplayEveryDeliveredMessageAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        foreach (var message in await context.Set<OutboxMessage>().ToListAsync(Token))
        {
            message.ProcessedOn = null;
        }

        await context.SaveChangesAsync(Token);
    }
}
