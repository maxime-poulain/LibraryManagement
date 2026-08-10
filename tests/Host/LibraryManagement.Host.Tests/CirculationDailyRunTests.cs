using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Host.Jobs;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using CirculationCopyId = LibraryManagement.Circulation.Domain.CopyId;
using CirculationEditionId = LibraryManagement.Circulation.Domain.EditionId;
using HoldingsCopyId = LibraryManagement.Holdings.Domain.Copies.CopyId;
using HoldingsEditionId = LibraryManagement.Holdings.Domain.Copies.EditionId;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// The daily process, run by the host that owns it: seven commands, seven scopes, seven saves, and
/// the outbox as the record of what the day announced.
/// </summary>
/// <remarks>
/// <para>
/// The test that matters here is the second one. <em>Running it twice must change nothing and
/// notify nobody twice</em> is the literal requirement the tactical design puts on the scheduled
/// process, and the outbox is where a broken promise would show: a second run that announced
/// anything would leave rows behind.
/// </para>
/// <para>
/// Against the real composition, which is what moving this out of the composition tests bought.
/// The unfulfillability sweep asks Holdings whether an edition can still serve, and the debt
/// reconciliation asks Charges what a borrower owes — questions that used to be answered by
/// stand-ins and are now answered by the modules. So the day's setup has to give Holdings a copy of
/// each edition somebody is waiting for, exactly as a library would have.
/// </para>
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

    private TheHost _host = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_CirculationDailyRun_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await Databases.DropAsync(ConnectionString, Token);

        // A clock that does not move: none of the seven queries is testable against one that does.
        _host = new TheHost(
            ConnectionString,
            clock: new FrozenClock(new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero)));

        await ADaysWorthOfWorkAsync();
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    /// <summary>
    /// One loan or queue for each row of the design's table, written straight to the store: what
    /// the desk would have left behind over the previous month.
    /// </summary>
    private Task ADaysWorthOfWorkAsync()
        => _host.InScopeAsync(async services =>
        {
            var circulation = services.GetRequiredService<CirculationDbContext>();
            var holdings = services.GetRequiredService<HoldingsDbContext>();

            circulation.Add(ALoanDueOn(Today.AddDays(Policy.CourtesyReminderDaysBeforeDue)));
            circulation.Add(ALoanDueOn(Today.AddDays(-1)));
            circulation.Add(ALoanDueOn(Today.AddDays(-Policy.DeclaredLostAfterDays)));

            var awaitingToday = AQueueAwaitingPickup(Today);
            var overdueForPickup = AQueueAwaitingPickup(Today.AddDays(-1), withSomebodyNextInLine: true);

            circulation.Add(awaitingToday);
            circulation.Add(overdueForPickup);

            // A copy of each awaited edition, so the unfulfillability sweep finds something to
            // promise. Written as aggregates rather than acquired through the desk: what is being
            // set up is the state a month of work leaves, not the moments that produced it.
            holdings.Add(ACopyOf(awaitingToday.Id, "31234567890140"));
            holdings.Add(ACopyOf(overdueForPickup.Id, "31234567890141"));

            await circulation.SaveChangesAsync(Token);
            await holdings.SaveChangesAsync(Token);
        });

    private static Loan ALoanDueOn(DateOnly dueDate)
        => Loan.CheckOut(
            LoanId.Generate(),
            CirculationCopyId.Generate(),
            CirculationEditionId.Generate(),
            BorrowerId.Generate(),
            dueDate.AddDays(-Policy.LoanDurationInDays),
            Policy);

    private static HoldQueue AQueueAwaitingPickup(
        DateOnly deadline,
        bool withSomebodyNextInLine = false)
    {
        var queue = HoldQueue.For(CirculationEditionId.Generate());
        queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning);

        if (withSomebodyNextInLine)
        {
            queue.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning.AddHours(1));
        }

        queue.TrapOldestQueued(CirculationCopyId.Generate(), deadline, NobodyBlocked);

        return queue;
    }

    private static Copy ACopyOf(CirculationEditionId editionId, string barcode)
        => Copy.Acquire(
            HoldingsCopyId.Generate(),
            HoldingsEditionId.Create(editionId.Value),
            Barcode.Create(barcode).Match(code => code, _ => throw new InvalidOperationException()),
            Shelfmark.Create("843.912 SAI").Match(mark => mark, _ => throw new InvalidOperationException()),
            CopyCondition.Good,
            new DateOnly(2024, 3, 14));

    private Task<DailyRunOutcome> RunTheDayAsync()
        => _host.InScopeAsync(services =>
            services.GetRequiredService<CirculationDailyRun>().RunAsync(Token));

    private Task<List<string>> AnnouncedAsync()
        => _host.InScopeAsync(async services =>
        {
            var stored = await services.GetRequiredService<CirculationDbContext>()
                .Set<OutboxMessage>()
                .Select(message => message.Type)
                .ToListAsync(Token);

            // The stored type is an address — "{FullName}, {Assembly}" — and the last segment of
            // the name is what a reader of this test cares about.
            return stored.Select(type => type.Split(',')[0].Split('.')[^1]).ToList();
        });

    [Fact]
    public async Task TheRun_AnnouncesEveryMomentTheDayCallsFor()
    {
        // What the *run* announced, not what the table holds: setting the day up traps two copies,
        // and those two promises were made at the desk rather than by the day.
        var before = await AnnouncedAsync();

        var outcome = await RunTheDayAsync();

        outcome.Dispatched.ShouldBe(7);
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

        outcome.Dispatched.ShouldBe(7);
        outcome.Refused.ShouldBe(0);
        (await AnnouncedAsync()).Count.ShouldBe(settled.Count);
    }
}
