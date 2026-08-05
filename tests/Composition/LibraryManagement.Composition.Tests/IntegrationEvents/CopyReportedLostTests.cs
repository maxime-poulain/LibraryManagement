using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Extensions;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.Application.Copies.AcquireCopy;
using LibraryManagement.Holdings.Application.Copies.WithdrawCopy;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Extensions;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
// Both modules declare a CopyId and an EditionId carrying the same Guid, and neither knows the
// other's — the context map's "a cross-module reference is an identifier, redeclared locally", made
// visible here as the aliases a file touching both modules is forced to write.
using CirculationCopyId = LibraryManagement.Circulation.Domain.CopyId;
using CirculationEditionId = LibraryManagement.Circulation.Domain.EditionId;
using HoldingsCopyId = LibraryManagement.Holdings.Domain.Copies.CopyId;

namespace LibraryManagement.Composition.Tests.IntegrationEvents;

/// <summary>
/// The first fact to cross a module boundary, through the whole road it has to travel: an aggregate
/// in Circulation, an outbox row in Circulation's schema, a drain, a translation into a flat
/// contract, a subscriber in Holdings, and a copy whose status changed in Holdings' own transaction.
/// </summary>
/// <remarks>
/// <para>
/// What makes this worth a composition test rather than a unit one is precisely what a unit test
/// cannot reach: the subscriber runs inside <em>Circulation's</em> drain, whose save is on
/// Circulation's context. A subscriber that wrote to <c>HoldingsDbContext</c> directly would leave
/// the change tracked in a context nobody saves — and every unit test of that subscriber would still
/// pass. Only two real stores can tell the difference.
/// </para>
/// <para>
/// Catalog is registered because Holdings needs it: acquiring a copy asks Catalog's published
/// language whether the edition exists.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CopyReportedLostTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    // This class's own database, for the reason TwoModulesTests keeps one: a class that drops and
    // rebuilds schemas cannot share a database with the collection's other classes.
    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_CopyReportedLost_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        _provider = CompositionRoot.Services()
            .AddCatalogModule(options => options.UseSqlServer(ConnectionString))
            .AddHoldingsModule(options => options.UseSqlServer(ConnectionString))
            .AddCirculationModule(options => options.UseSqlServer(ConnectionString))
            .AddSingleton<IMemberBalance, NoChargesYet>()
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalog.Database.EnsureDeletedAsync(Token);
        await catalog.Database.EnsureCreatedAsync(Token);

        // The seam TwoModulesTests documents: EnsureCreated answers "already there" for the second
        // and third contexts over one database, leaving their schemas unbuilt.
        await CreateTablesOfAsync<HoldingsDbContext>(scope.ServiceProvider);
        await CreateTablesOfAsync<CirculationDbContext>(scope.ServiceProvider);
    }

    private static async Task CreateTablesOfAsync<TContext>(IServiceProvider services)
        where TContext : DbContext
    {
        var context = services.GetRequiredService<TContext>();
        await ((IRelationalDatabaseCreator)context.Database.GetService<IDatabaseCreator>())
            .CreateTablesAsync(Token);
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

    /// <summary>A copy on the shelf, with the edition Holdings insists on behind it.</summary>
    private async Task<Guid> ACopyAsync(string barcode)
    {
        var workId = Guid.CreateVersion7();
        (await DispatchAsync(new RegisterWorkCommand(workId, "La Horde du Contrevent", [])))
            .HasErrors().ShouldBeFalse();

        var editionId = Guid.CreateVersion7();
        (await DispatchAsync(new RegisterEditionCommand(editionId, workId, null)))
            .HasErrors().ShouldBeFalse();

        var copyId = Guid.CreateVersion7();
        (await DispatchAsync(new AcquireCopyCommand(
                copyId, editionId, barcode, "843.912 SAI", new DateOnly(2024, 3, 14))))
            .HasErrors().ShouldBeFalse();

        return copyId;
    }

    /// <summary>
    /// A loan Circulation has given up on, written straight to its store: what the daily run leaves
    /// behind, without running the day to get there.
    /// </summary>
    private async Task ALoanGivenUpOnAsync(Guid copyId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();

        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CirculationCopyId.Create(copyId),
            CirculationEditionId.Generate(),
            BorrowerId.Generate(),
            Today.AddDays(-CirculationPolicy.Current.DeclaredLostAfterDays),
            CirculationPolicy.Current);

        loan.DeclareLost();

        context.Add(loan);
        await context.SaveChangesAsync(Token);
    }

    private Task<OutboxDrainOutcome> DrainCirculationAsync()
        => _provider.GetRequiredService<OutboxProcessor<CirculationDbContext>>().ProcessAsync(Token);

    private Task<CopyStatus> StatusOfAsync(Guid copyId)
        => InScopeAsync(async services =>
        {
            var copy = await services.GetRequiredService<HoldingsDbContext>()
                .Set<Copy>()
                .SingleAsync(stored => stored.Id == HoldingsCopyId.Create(copyId), Token);

            return copy.Status;
        });

    [Fact]
    public async Task AFactAnnouncedByCirculation_ChangesACopyInHoldings()
    {
        var copyId = await ACopyAsync("31234567890120");
        await ALoanGivenUpOnAsync(copyId);

        (await StatusOfAsync(copyId)).ShouldBe(CopyStatus.InService, "nothing has been drained yet");

        var outcome = await DrainCirculationAsync();

        outcome.Blockage.ShouldBeNull();
        outcome.Delivered.ShouldBeGreaterThan(0);

        // Holdings decided this for itself, in its own transaction, from a fact that named no status.
        (await StatusOfAsync(copyId)).ShouldBe(CopyStatus.Lost);
    }

    [Fact]
    public async Task TheSameFactDeliveredTwice_LeavesTheCopyAsItIs()
    {
        // Delivery beyond a module is at-least-once, and this is the shape that makes it survivable:
        // Copy.DeclareLost answers success for a copy already lost, so a replay converges instead of
        // failing. A subscriber whose operation is not idempotent owes the contract's event identity
        // the same service.
        var copyId = await ACopyAsync("31234567890121");
        await ALoanGivenUpOnAsync(copyId);

        await DrainCirculationAsync();
        await ReplayEveryDeliveredMessageAsync();

        var outcome = await DrainCirculationAsync();

        outcome.Blockage.ShouldBeNull();
        (await StatusOfAsync(copyId)).ShouldBe(CopyStatus.Lost);
    }

    [Fact]
    public async Task ASubscriberThatRefuses_LeavesTheRowUnmarkedForTheNextRun()
    {
        // The failure path the whole mechanism rests on. A withdrawn copy cannot be declared lost,
        // so Holdings refuses — and the refusal must not be mistaken for a reaction: Circulation's
        // row stays pending, with the attempt recorded, and the next run takes the same head again.
        var copyId = await ACopyAsync("31234567890122");
        (await DispatchAsync(new WithdrawCopyCommand(copyId))).HasErrors().ShouldBeFalse();

        await ALoanGivenUpOnAsync(copyId);

        var outcome = await DrainCirculationAsync();

        outcome.Blockage.ShouldNotBeNull().Attempts.ShouldBe(1);
        outcome.Blockage.Dead.ShouldBeFalse();

        await using var scope = _provider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<CirculationDbContext>()
            .Set<OutboxMessage>()
            .SingleAsync(message => message.Id == outcome.Blockage.MessageId, Token);

        stored.ProcessedOn.ShouldBeNull("a refusal is not a delivery");
        stored.Error.ShouldNotBeNull();

        (await StatusOfAsync(copyId)).ShouldBe(CopyStatus.Withdrawn);
    }

    /// <summary>
    /// Clears the processed marks, so the next drain redelivers everything the last one delivered —
    /// the crash-between-two-transactions this mechanism promises to survive, made deterministic.
    /// </summary>
    private async Task ReplayEveryDeliveredMessageAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();

        foreach (var message in await context.Set<OutboxMessage>().ToListAsync(Token))
        {
            message.ProcessedOn = null;
        }

        await context.SaveChangesAsync(Token);
    }
}
