using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Extensions;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.Scheduling;

/// <summary>
/// The daily process, run through the container the way a host would: five commands, five scopes,
/// five saves, and the outbox as the record of what the day announced.
/// </summary>
/// <remarks>
/// The test that matters here is the second one. <em>Running it twice must change nothing and
/// notify nobody twice</em> is the literal requirement the tactical design puts on the scheduled
/// process, and the outbox is where a broken promise would show: a second run that announced
/// anything would leave rows behind.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CirculationDailyRunTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlySet<BorrowerId> NobodyBlocked = new HashSet<BorrowerId>();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CirculationPolicy Policy => CirculationPolicy.Current;

    private ServiceProvider _provider = null!;

    // A database of this class's own, for the reason TwoModulesTests keeps one: a class that drops
    // and rebuilds a schema cannot share a database with the collection's other classes.
    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_CirculationDailyRun_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        // The frozen clock goes in before the module, because the shared store registration adds
        // the system clock only if nobody else has: none of the five queries is testable against a
        // clock that keeps moving.
        _provider = CompositionRoot.Services()
            .AddSingleton<TimeProvider>(new FrozenClock(new DateTimeOffset(
                Today, TimeOnly.MinValue, TimeSpan.Zero)))
            .AddCirculationModule(options => options.UseSqlServer(ConnectionString))
            .AddSingleton<IMemberBalance, NoChargesYet>()
            .AddTransient<CirculationDailyRun>()
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();
        await context.Database.EnsureDeletedAsync(Token);
        await context.Database.EnsureCreatedAsync(Token);

        await ADaysWorthOfWorkAsync();
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    /// <summary>
    /// One loan or queue for each row of the design's table, written straight to the store: what
    /// the desk would have left behind over the previous month.
    /// </summary>
    private async Task ADaysWorthOfWorkAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();

        context.Add(ALoanDueOn(Today.AddDays(Policy.CourtesyReminderDaysBeforeDue)));
        context.Add(ALoanDueOn(Today.AddDays(-1)));
        context.Add(ALoanDueOn(Today.AddDays(-Policy.DeclaredLostAfterDays)));

        context.Add(AQueueAwaitingPickup(Today));
        context.Add(AQueueAwaitingPickup(Today.AddDays(-1), withSomebodyNextInLine: true));

        await context.SaveChangesAsync(Token);
    }

    private static Loan ALoanDueOn(DateOnly dueDate)
        => Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            EditionId.Generate(),
            BorrowerId.Generate(),
            dueDate.AddDays(-Policy.LoanDurationInDays),
            Policy);

    private static HoldQueue AQueueAwaitingPickup(
        DateOnly deadline,
        bool withSomebodyNextInLine = false)
    {
        var queue = HoldQueue.For(EditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);

        if (withSomebodyNextInLine)
        {
            queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning.AddHours(1));
        }

        queue.TrapOldestQueued(CopyId.Generate(), deadline, NobodyBlocked);

        return queue;
    }

    private Task<DailyRunOutcome> RunTheDayAsync()
        => _provider.GetRequiredService<CirculationDailyRun>().RunAsync(Token);

    private async Task<List<string>> AnnouncedAsync()
    {
        await using var scope = _provider.CreateAsyncScope();

        var stored = await scope.ServiceProvider.GetRequiredService<CirculationDbContext>()
            .Set<OutboxMessage>()
            .Select(message => message.Type)
            .ToListAsync(Token);

        // The stored type is an address — "{FullName}, {Assembly}" — and the last segment of the
        // name is what a reader of this test cares about.
        return stored
            .Select(type => type.Split(',')[0].Split('.')[^1])
            .ToList();
    }

    [Fact]
    public async Task TheRun_AnnouncesEveryMomentTheDayCallsFor()
    {
        // What the *run* announced, not what the table holds: setting the day up traps two copies,
        // and those two promises were made at the desk rather than by the day.
        var before = await AnnouncedAsync();

        var outcome = await RunTheDayAsync();

        outcome.Dispatched.ShouldBe(5);
        outcome.Refused.ShouldBe(0);

        var announced = TheDifferenceBetween(before, await AnnouncedAsync());

        // The courtesy reminder, the two loans past their date, the declaration, and both hold
        // moments — the design's table, top to bottom.
        announced[nameof(LoanDueSoon)].ShouldBe(1);
        announced[nameof(LoanBecameOverdue)].ShouldBe(2);
        announced[nameof(LoanDeclaredLost)].ShouldBe(1);
        announced[nameof(HoldExpiringSoon)].ShouldBe(1);
        announced[nameof(HoldExpired)].ShouldBe(1);

        // The released copy did not go back to the shelf: it moved on to whoever was next.
        announced[nameof(HoldReadyForPickup)].ShouldBe(1);
    }

    private static Dictionary<string, int> TheDifferenceBetween(
        IReadOnlyCollection<string> before,
        IReadOnlyCollection<string> after)
        => after
            .GroupBy(type => type)
            .ToDictionary(
                group => group.Key,
                group => group.Count() - before.Count(type => type == group.Key));

    [Fact]
    public async Task TheRun_Twice_ChangesNothingAndNotifiesNobodyTwice()
    {
        await RunTheDayAsync();
        var afterTheFirst = await AnnouncedAsync();

        var outcome = await RunTheDayAsync();
        var afterTheSecond = await AnnouncedAsync();

        outcome.Refused.ShouldBe(0);
        afterTheSecond.Count.ShouldBe(afterTheFirst.Count);
    }

    [Fact]
    public async Task TheRun_OnAQuietDay_AnnouncesNothingAndStillSucceeds()
    {
        // Every loan and queue has been dealt with; the day still runs, and says nothing.
        await RunTheDayAsync();
        var settled = await AnnouncedAsync();

        var outcome = await RunTheDayAsync();

        outcome.Dispatched.ShouldBe(5);
        outcome.Refused.ShouldBe(0);
        (await AnnouncedAsync()).Count.ShouldBe(settled.Count);
    }

    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
