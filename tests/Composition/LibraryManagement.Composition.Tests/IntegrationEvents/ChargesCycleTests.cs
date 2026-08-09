using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Extensions;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Extensions;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
// Three modules declare a LoanId and a CopyId carrying the same Guid, and none knows another's.
// A file that touches all three is forced to say which it means, which is the context map's
// "an identifier, redeclared locally" made visible.
using ChargesMemberId = LibraryManagement.Charges.Domain.Accounts.MemberId;
using CirculationCopyId = LibraryManagement.Circulation.Domain.CopyId;
using CirculationLoanId = LibraryManagement.Circulation.Domain.Loans.LoanId;

namespace LibraryManagement.Composition.Tests.IntegrationEvents;

/// <summary>
/// The one cycle the context map draws, turning both ways in one container.
/// </summary>
/// <remarks>
/// <para>
/// Circulation announces a fact, Charges prices it, Charges announces an amount, and Circulation
/// judges it and acts. Every step crosses a module boundary, a transaction and a drain, and no unit
/// test of any single piece can tell whether the chain holds — which is the whole reason this file
/// exists.
/// </para>
/// <para>
/// <c>NoChargesYet</c> is deliberately absent. The composition that registers Charges has a real
/// answer to the balance question, and registering a stand-in beside it would let one silently win
/// by registration order.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class ChargesCycleTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CirculationPolicy Policy => CirculationPolicy.Current;

    private ServiceProvider _provider = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_ChargesCycle_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        _provider = CompositionRoot.Services()
            .AddSingleton<TimeProvider>(new FrozenClock(
                new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero)))
            .AddCirculationModule(options => options.UseSqlServer(ConnectionString))
            .AddChargesModule(options => options.UseSqlServer(ConnectionString))
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();

        var circulation = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
        await circulation.Database.EnsureDeletedAsync(Token);
        await circulation.Database.EnsureCreatedAsync(Token);

        // The seam TwoModulesTests documents: EnsureCreated answers "already there" for a second
        // context over one database, leaving its schema unbuilt.
        var charges = scope.ServiceProvider.GetRequiredService<ChargesDbContext>();
        await ((IRelationalDatabaseCreator)charges.Database.GetService<IDatabaseCreator>())
            .CreateTablesAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private Task<OutboxDrainOutcome> DrainCirculationAsync()
        => _provider.GetRequiredService<OutboxProcessor<CirculationDbContext>>().ProcessAsync(Token);

    private Task<OutboxDrainOutcome> DrainChargesAsync()
        => _provider.GetRequiredService<OutboxProcessor<ChargesDbContext>>().ProcessAsync(Token);

    /// <summary>A loan returned late, written straight to Circulation's store.</summary>
    private async Task<BorrowerId> ALateReturnAsync(int daysLate)
    {
        var borrowerId = BorrowerId.Generate();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();

        var loan = Loan.CheckOut(
            CirculationLoanId.Generate(),
            CirculationCopyId.Generate(),
            EditionId.Generate(),
            borrowerId,
            Today.AddDays(-Policy.LoanDurationInDays - daysLate),
            Policy);

        loan.Return(Today, Policy);

        context.Add(loan);
        await context.SaveChangesAsync(Token);

        return borrowerId;
    }

    private async Task<decimal> OwedByAsync(BorrowerId borrowerId)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IMemberBalance>()
            .OwedByAsync(borrowerId.Value, Token);
    }

    [Fact]
    public async Task ALateReturn_TravelsFromCirculationToAPriceInCharges()
    {
        var borrowerId = await ALateReturnAsync(daysLate: 3);

        (await OwedByAsync(borrowerId)).ShouldBe(0m, "nothing has been drained yet");

        var outcome = await DrainCirculationAsync();

        outcome.Blockage.ShouldBeNull();
        (await OwedByAsync(borrowerId)).ShouldBe(0.60m);
    }

    [Fact]
    public async Task AReturnOnTime_TravelsAndIsPricedAtNothing()
    {
        // The fact leaves Circulation whatever the delay, because a grace period is the tariff's
        // business. Charges decides there is nothing to charge, and succeeds.
        var borrowerId = await ALateReturnAsync(daysLate: 0);

        var outcome = await DrainCirculationAsync();

        outcome.Blockage.ShouldBeNull();
        (await OwedByAsync(borrowerId)).ShouldBe(0m);
    }

    [Fact]
    public async Task TheSameLateReturn_DrainedTwice_IsPricedOnce()
    {
        var borrowerId = await ALateReturnAsync(daysLate: 3);

        await DrainCirculationAsync();
        await ReplayEverythingAsync<CirculationDbContext>();
        await DrainCirculationAsync();

        (await OwedByAsync(borrowerId)).ShouldBe(0.60m);
    }

    [Fact]
    public async Task ADebtIncurredInCharges_CostsTheBorrowerTheirPlaceInCirculation()
    {
        // The cycle closing. Circulation announced a return, Charges priced it, Charges announced
        // the amount, and Circulation judged it against its own threshold and acted — four
        // transactions and two drains, none of which knows the others exist.
        var borrowerId = await ALateReturnAsync(daysLate: 3);

        await using (var scope = _provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
            var queue = HoldQueue.For(EditionId.Generate());
            queue.PlaceHold(HoldId.Generate(), borrowerId, ThisMorning);
            context.Add(queue);
            await context.SaveChangesAsync(Token);
        }

        await DrainCirculationAsync();
        var draining = await DrainChargesAsync();

        draining.Blockage.ShouldBeNull();

        (await PlacesHeldByAsync(borrowerId)).ShouldBe(0, "a borrower who owes money holds no places");
    }

    [Fact]
    public async Task AMemberWhoOwesNothing_KeepsTheirPlace()
    {
        // The other half of the judgement, and the one a pair of transition events would have got
        // right by accident: a movement that crosses nothing changes nothing.
        var borrowerId = BorrowerId.Generate();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
            var queue = HoldQueue.For(EditionId.Generate());
            queue.PlaceHold(HoldId.Generate(), borrowerId, ThisMorning);
            context.Add(queue);
            await context.SaveChangesAsync(Token);
        }

        await DrainCirculationAsync();
        await DrainChargesAsync();

        (await PlacesHeldByAsync(borrowerId)).ShouldBe(1);
    }

    [Fact]
    public async Task APaymentClearingTheBalance_AnnouncesTheMovementBack()
    {
        // Nothing in Circulation reacts to a balance returning to nothing — the borrower is simply
        // able to act again — but the movement is announced all the same, because this context does
        // not know what a threshold is.
        var borrowerId = await ALateReturnAsync(daysLate: 3);
        await DrainCirculationAsync();

        await using (var scope = _provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ChargesDbContext>();
            var account = await new MemberAccountRepository(context)
                .GetByMemberAsync(ChargesMemberId.Create(borrowerId.Value), Token);
            account!.TakePayment(Money.Of(0.60m));
            await context.SaveChangesAsync(Token);
        }

        var outcome = await DrainChargesAsync();

        outcome.Blockage.ShouldBeNull();
        (await OwedByAsync(borrowerId)).ShouldBe(0m);
    }

    private async Task ReplayEverythingAsync<TContext>()
        where TContext : DbContext
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        foreach (var message in await context.Set<OutboxMessage>().ToListAsync(Token))
        {
            message.ProcessedOn = null;
        }

        await context.SaveChangesAsync(Token);
    }

    /// <summary>
    /// How many places a borrower occupies, across every queue.
    /// </summary>
    /// <remarks>
    /// The queues are loaded and the claims counted in memory, because a claim is an owned entity
    /// and Entity Framework refuses to track one without its owner. Counting in the database would
    /// mean AsNoTracking, which would be a workaround for a query this test has no reason to make.
    /// </remarks>
    private async Task<int> PlacesHeldByAsync(BorrowerId borrowerId)
    {
        await using var scope = _provider.CreateAsyncScope();

        var queues = await scope.ServiceProvider.GetRequiredService<CirculationDbContext>()
            .Set<HoldQueue>()
            .Include(queue => queue.Holds)
            .ToListAsync(Token);

        return queues.Sum(queue => queue.Holds.Count(hold => hold.BorrowerId == borrowerId));
    }

    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
