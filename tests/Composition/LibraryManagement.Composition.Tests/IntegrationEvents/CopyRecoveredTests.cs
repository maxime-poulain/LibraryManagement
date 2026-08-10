using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Migrations.SqlServer;
using LibraryManagement.Charges.Infrastructure.Extensions;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Charges.Migrations.SqlServer;
using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Extensions;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.Migrations.SqlServer;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.Application.Copies.FindCopy;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Extensions;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Holdings.Migrations.SqlServer;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CirculationCopyId = LibraryManagement.Circulation.Domain.CopyId;
using CirculationEditionId = LibraryManagement.Circulation.Domain.EditionId;
using HoldingsCopyId = LibraryManagement.Holdings.Domain.Copies.CopyId;
using HoldingsEditionId = LibraryManagement.Holdings.Domain.Copies.EditionId;

namespace LibraryManagement.Composition.Tests.IntegrationEvents;

/// <summary>
/// The recovered copy, through everything its reappearance settles: Holdings finds it, Charges
/// cancels the replacement still owed, Circulation announces the lateness the loan had frozen,
/// and Charges prices that — one desk act, three modules, two drains.
/// </summary>
/// <remarks>
/// The scenario is the audit's own: without this chain, a copy brought back on day forty-five
/// cost its borrower nothing — the €25 cancelled by the find, the fine never assessed because
/// the loan ended without a return — while day twenty-nine cost €5.80. What this asserts is the
/// repaired arithmetic: the recovery leaves exactly the fine a return on the write-off day would
/// have left, and the replacement charge is gone.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CopyRecoveredTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_CopyRecovered_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        _provider = CompositionRoot.Services()
            .AddSingleton<TimeProvider>(new FrozenClock(
                new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero)))
            .AddCatalogModule(options => options.UseCatalogSqlServer(ConnectionString))
            .AddHoldingsModule(options => options.UseHoldingsSqlServer(ConnectionString))
            .AddCirculationModule(options => options.UseCirculationSqlServer(ConnectionString))
            .AddChargesModule(options => options.UseChargesSqlServer(ConnectionString))
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await catalog.Database.EnsureDeletedAsync(Token);
        await catalog.Database.MigrateAsync(Token);

        await MigrateAsync<HoldingsDbContext>(scope.ServiceProvider);
        await MigrateAsync<CirculationDbContext>(scope.ServiceProvider);
        await MigrateAsync<ChargesDbContext>(scope.ServiceProvider);
    }

    private static async Task MigrateAsync<TContext>(IServiceProvider services)
        where TContext : DbContext
    {
        var context = services.GetRequiredService<TContext>();
        await context.Database.MigrateAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private Task<OutboxDrainOutcome> DrainAsync<TContext>() where TContext : DbContext
        => _provider.GetRequiredService<OutboxProcessor<TContext>>().ProcessAsync(Token);

    /// <summary>Drains every module until a full pass delivers nothing, so chains settle.</summary>
    private async Task DrainEverythingAsync()
    {
        int delivered;
        do
        {
            delivered = (await DrainAsync<CirculationDbContext>()).Delivered
                + (await DrainAsync<HoldingsDbContext>()).Delivered
                + (await DrainAsync<ChargesDbContext>()).Delivered;
        }
        while (delivered > 0);
    }

    [Fact]
    public async Task AFoundCopy_CancelsTheReplacement_AndPricesTheFrozenLateness()
    {
        var copyId = Guid.CreateVersion7();
        var borrowerId = BorrowerId.Generate();

        // A copy the library holds as lost, written straight to Holdings' store.
        await using (var scope = _provider.CreateAsyncScope())
        {
            var holdings = scope.ServiceProvider.GetRequiredService<HoldingsDbContext>();
            var copy = Copy.Acquire(
                HoldingsCopyId.Create(copyId),
                HoldingsEditionId.Generate(),
                Barcode.Create("31234567890130").Match(b => b, _ => throw new InvalidOperationException()),
                Shelfmark.Create("843.912 SAI").Match(m => m, _ => throw new InvalidOperationException()),
                CopyCondition.Good,
                new DateOnly(2024, 3, 14));
            copy.DeclareLost();
            holdings.Add(copy);
            await holdings.SaveChangesAsync(Token);
        }

        // The loan the library gave up on: due thirty days before it was declared lost, so the
        // frozen lateness is thirty open days — €6.00 at the tariff, under the €10 cap.
        await using (var scope = _provider.CreateAsyncScope())
        {
            var circulation = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
            var loan = Loan.CheckOut(
                LoanId.Generate(),
                CirculationCopyId.Create(copyId),
                CirculationEditionId.Generate(),
                borrowerId,
                Today.AddDays(-CirculationPolicy.Current.DeclaredLostAfterDays
                    - CirculationPolicy.Current.LoanDurationInDays),
                CirculationPolicy.Current);
            loan.DeclareLost(Today);
            circulation.Add(loan);
            await circulation.SaveChangesAsync(Token);
        }

        // The write-off reaches Charges: the borrower owes the replacement.
        await DrainEverythingAsync();
        (await OwedAsync(borrowerId)).ShouldBe(25.00m);

        // The desk finds the copy.
        await using (var scope = _provider.CreateAsyncScope())
        {
            var outcome = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
                .DispatchAsync(new FindCopyCommand(copyId), Token);
            outcome.HasErrors().ShouldBeFalse();
        }

        // The recovery reaches everyone it concerns.
        await DrainEverythingAsync();

        // The replacement is cancelled, the frozen lateness is priced, and the arithmetic no
        // longer rewards keeping a book past the write-off: thirty open days at €0.20.
        (await OwedAsync(borrowerId)).ShouldBe(6.00m);
        (await StatusOfAsync(copyId)).ShouldBe(CopyStatus.InService);
    }

    private async Task<decimal> OwedAsync(BorrowerId borrowerId)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMemberBalance>()
            .OwedByAsync(borrowerId.Value, Token);
    }

    private async Task<CopyStatus> StatusOfAsync(Guid copyId)
    {
        await using var scope = _provider.CreateAsyncScope();
        var copy = await scope.ServiceProvider.GetRequiredService<HoldingsDbContext>()
            .Set<Copy>()
            .SingleAsync(stored => stored.Id == HoldingsCopyId.Create(copyId), Token);
        return copy.Status;
    }

    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
