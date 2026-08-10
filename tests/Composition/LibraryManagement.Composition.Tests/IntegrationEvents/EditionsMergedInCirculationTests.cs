using LibraryManagement.Catalog.Application.Editions.MergeEditions;
using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Migrations.SqlServer;
using LibraryManagement.Circulation.Domain;
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
/// The same fact reaching its second consumer: Catalog merges two records, and the loans still out
/// on the absorbed one answer to the survivor.
/// </summary>
/// <remarks>
/// <para>
/// A file of its own rather than a case in <c>EditionsMergedTests</c>, for the reason
/// <c>CopyReportedLostTests</c> and <c>CopyRecoveredTests</c> are two files: what is under test is a
/// passage between two named modules, and each has its own composition and its own store to read.
/// </para>
/// <para>
/// It proves the half a unit test cannot reach — the subscriber runs inside <em>Catalog's</em>
/// drain, whose save is on Catalog's context — and one thing more: that an ended loan does not move.
/// That rule lives in the aggregate, but only two real stores show it holding across the whole road.
/// </para>
/// <para>
/// Holdings is absent, deliberately. This composition has no acquisition in it: the loans are
/// written straight to Circulation's store, so nothing here asks whether a copy may be lent.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EditionsMergedInCirculationTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    // This class's own database: one that drops and rebuilds schemas cannot share with the
    // collection's other classes.
    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_EditionsMergedInCirculation_Tests",
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

    /// <summary>
    /// A loan written straight to Circulation's store, since what is under test is the reaction and
    /// not the desk that produced the loan.
    /// </summary>
    private async Task<LoanId> ALoanOfAsync(Guid editionId, bool returned = false)
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

        if (returned)
        {
            loan.Return(Today, CirculationPolicy.Current);
        }

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

    [Fact]
    public async Task AMergeInCatalog_MovesTheLoansStillOut()
    {
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);

        var first = await ALoanOfAsync(absorbed);
        var second = await ALoanOfAsync(absorbed);
        var bystander = await ALoanOfAsync(await AnEditionOfAsync(workId));

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        (await EditionOfAsync(first)).ShouldBe(absorbed, "nothing has been drained yet");

        var outcome = await DrainCatalogAsync();

        outcome.Blockage.ShouldBeNull();
        outcome.Delivered.ShouldBeGreaterThan(0);

        // Circulation decided this for itself, in its own transaction, from a fact that said
        // nothing about loans.
        (await EditionOfAsync(first)).ShouldBe(surviving);
        (await EditionOfAsync(second)).ShouldBe(surviving);

        (await EditionOfAsync(bystander)).ShouldNotBe(surviving);
    }

    [Fact]
    public async Task ALoanThatEndedBeforeTheMerge_KeepsWhatWasBorrowed()
    {
        // Where this context parts company with Holdings, which moves even a weeded copy. A copy
        // record is a present-tense fact about an object; a returned loan is the account of
        // something that happened, and no later correction of the catalog changes what happened.
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);

        var history = await ALoanOfAsync(absorbed, returned: true);

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        var outcome = await DrainCatalogAsync();

        outcome.Blockage.ShouldBeNull();
        (await EditionOfAsync(history)).ShouldBe(absorbed);
    }

    [Fact]
    public async Task TheSameFactDeliveredTwice_LeavesTheLoansWhereTheFirstDeliveryPutThem()
    {
        // Delivery beyond a module is at-least-once, and this subscriber survives it without a
        // deduplication table: the command sweeps by the absorbed identifier, which after the first
        // pass is on no live loan at all.
        var workId = await AWorkAsync();
        var absorbed = await AnEditionOfAsync(workId);
        var surviving = await AnEditionOfAsync(workId);
        var loanId = await ALoanOfAsync(absorbed);

        (await DispatchAsync(new MergeEditionsCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        await DrainCatalogAsync();
        await ReplayEveryDeliveredMessageAsync();

        var outcome = await DrainCatalogAsync();

        outcome.Blockage.ShouldBeNull();
        (await EditionOfAsync(loanId)).ShouldBe(surviving);
    }

    [Fact]
    public async Task AMergeOfAnEditionWithNothingOut_IsDeliveredAllTheSame()
    {
        // The ordinary case, and the one a refusal would turn into a message the drain replays
        // forever.
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
