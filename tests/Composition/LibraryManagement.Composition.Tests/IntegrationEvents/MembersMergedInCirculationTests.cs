using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Extensions;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.Migrations.SqlServer;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Members.Application.Members.EnrollMember;
using LibraryManagement.Members.Application.Members.MergeMembers;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.Extensions;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Members.Migrations.SqlServer;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.IntegrationEvents;

/// <summary>
/// The first fact Members states on its own initiative, reaching its first consumer: two files are
/// merged at the Members desk, and Circulation repoints the loans not yet answered for while
/// combining the person's claims in every queue.
/// </summary>
/// <remarks>
/// <para>
/// It proves the half a unit test cannot reach — both subscribers run inside <em>Members'</em>
/// drain, whose save is on Members' context — and one thing more: that the loan sweep's scope is
/// the third cut it claims to be. A returned loan and a recovered one keep the absorbed
/// identifier; an active one and a written-off one still waiting for its copy do not. Only two
/// real stores show that cut holding across the whole road.
/// </para>
/// <para>
/// Holdings and Catalog are absent, deliberately: nothing here accessions a copy or registers an
/// edition. The loans and the queues are written straight to Circulation's store, because what is
/// under test is the reaction and not the desk that produced the state.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MembersMergedInCirculationTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_MembersMergedInCirculation_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        _provider = CompositionRoot.Services()
            .AddSingleton<TimeProvider>(new FrozenClock(
                new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero)))
            .AddMembersModule(options => options.UseMembersSqlServer(ConnectionString))
            .AddCirculationModule(options => options.UseCirculationSqlServer(ConnectionString))
            .AddSingleton<IMemberBalance, NoChargesYet>()
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();

        var members = scope.ServiceProvider.GetRequiredService<MembersDbContext>();
        await members.Database.EnsureDeletedAsync(Token);
        await members.Database.MigrateAsync(Token);

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

    private Task<OutboxDrainOutcome> DrainMembersAsync()
        => _provider.GetRequiredService<OutboxProcessor<MembersDbContext>>().ProcessAsync(Token);

    private async Task<Guid> AnEnrolledMemberAsync(string cardNumber)
    {
        var memberId = Guid.CreateVersion7();

        (await DispatchAsync(new EnrollMemberCommand(
                memberId,
                "Antoine",
                "Doinel",
                new DateOnly(1990, 5, 1),
                MemberCategory.Adult,
                cardNumber,
                Email: "antoine.doinel@example.org")))
            .HasErrors().ShouldBeFalse();

        return memberId;
    }

    /// <summary>
    /// A loan written straight to Circulation's store, in the state the case needs: what is under
    /// test is the reaction, and the scope of the sweep is exactly which states move.
    /// </summary>
    private async Task<LoanId> ALoanOfAsync(Guid memberId, string state = "active")
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();

        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            EditionId.Generate(),
            BorrowerId.Create(memberId),
            Today,
            CirculationPolicy.Current);

        switch (state)
        {
            case "returned":
                loan.Return(Today, CirculationPolicy.Current);
                break;
            case "writtenOff":
                loan.DeclareLost(Today.AddDays(45));
                break;
            case "recovered":
                loan.DeclareLost(Today.AddDays(45));
                loan.RecordRecovery(Today.AddDays(90), CirculationPolicy.Current);
                break;
        }

        context.Add(loan);
        await context.SaveChangesAsync(Token);

        return loan.Id;
    }

    private async Task<Guid> AQueueAsync(params (Guid Member, int HoursIn)[] claims)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CirculationDbContext>();

        var queue = HoldQueue.For(EditionId.Generate());

        foreach (var (member, hoursIn) in claims)
        {
            queue.PlaceHold(
                HoldId.Generate(), BorrowerId.Create(member), ThisMorning.AddHours(hoursIn));
        }

        context.Add(queue);
        await context.SaveChangesAsync(Token);

        return queue.Id.Value;
    }

    private Task<Guid> BorrowerOfAsync(LoanId loanId)
        => InScopeAsync(async services =>
        {
            var loan = await services.GetRequiredService<CirculationDbContext>()
                .Set<Loan>()
                .SingleAsync(stored => stored.Id == loanId, Token);

            return loan.BorrowerId.Value;
        });

    private Task<HoldQueue?> QueueOfAsync(Guid editionId)
        => InScopeAsync(async services => await new HoldQueueRepository(
                services.GetRequiredService<CirculationDbContext>())
            .GetByEditionAsync(EditionId.Create(editionId), Token));

    [Fact]
    public async Task AMergeAtTheDesk_MovesTheLoansNotYetAnsweredFor()
    {
        var absorbed = await AnEnrolledMemberAsync("20260000101");
        var surviving = await AnEnrolledMemberAsync("20260000102");

        var active = await ALoanOfAsync(absorbed);
        var writtenOff = await ALoanOfAsync(absorbed, "writtenOff");
        var returned = await ALoanOfAsync(absorbed, "returned");
        var recovered = await ALoanOfAsync(absorbed, "recovered");

        (await DispatchAsync(new MergeMembersCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        (await BorrowerOfAsync(active)).ShouldBe(absorbed, "nothing has been drained yet");

        var outcome = await DrainMembersAsync();

        outcome.Blockage.ShouldBeNull();
        outcome.Delivered.ShouldBeGreaterThan(0);

        // Circulation decided this for itself, in its own transaction, from a fact that said
        // nothing about loans — and the cut is the third one: the borrower is asked about until
        // the loan is answered for, which reaches past live loans and stops short of settled ones.
        (await BorrowerOfAsync(active)).ShouldBe(surviving);
        (await BorrowerOfAsync(writtenOff)).ShouldBe(surviving);

        (await BorrowerOfAsync(returned)).ShouldBe(absorbed);
        (await BorrowerOfAsync(recovered)).ShouldBe(absorbed);
    }

    [Fact]
    public async Task AMergeAtTheDesk_CombinesTheClaimsInEveryQueue()
    {
        var absorbed = await AnEnrolledMemberAsync("20260000111");
        var surviving = await AnEnrolledMemberAsync("20260000112");
        var bystander = await AnEnrolledMemberAsync("20260000113");

        // One queue where both files wait — the collision — and one where only the absorbed does.
        var contested = await AQueueAsync((absorbed, 0), (surviving, 2), (bystander, 1));
        var quiet = await AQueueAsync((absorbed, 0));

        (await DispatchAsync(new MergeMembersCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        (await DrainMembersAsync()).Blockage.ShouldBeNull();

        // The earliest claim survived, whichever file it came from, and the person keeps exactly
        // one place among the people around them.
        var merged = await QueueOfAsync(contested);
        merged!.QueuedBorrowersInOrder()
            .ShouldBe([BorrowerId.Create(surviving), BorrowerId.Create(bystander)]);
        merged.Holds.Single(hold => hold.BorrowerId == BorrowerId.Create(surviving))
            .PlacedOn.ShouldBe(ThisMorning);

        (await QueueOfAsync(quiet))!.Holds.ShouldHaveSingleItem()
            .BorrowerId.ShouldBe(BorrowerId.Create(surviving));
    }

    [Fact]
    public async Task OneAnnouncement_ReachesBothOfThisModulesSubscribers()
    {
        // The loans and the queues are separate units of consistency, so the module registers two
        // subscribers for one contract. Only a composition can show that both actually run.
        var absorbed = await AnEnrolledMemberAsync("20260000121");
        var surviving = await AnEnrolledMemberAsync("20260000122");

        var loanId = await ALoanOfAsync(absorbed);
        var editionId = await AQueueAsync((absorbed, 0));

        (await DispatchAsync(new MergeMembersCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        (await DrainMembersAsync()).Blockage.ShouldBeNull();

        (await BorrowerOfAsync(loanId)).ShouldBe(surviving);
        (await QueueOfAsync(editionId))!.Holds.ShouldHaveSingleItem()
            .BorrowerId.ShouldBe(BorrowerId.Create(surviving));
    }

    [Fact]
    public async Task TheSameFactDeliveredTwice_LeavesEverythingWhereTheFirstDeliveryPutIt()
    {
        // Delivery beyond a module is at-least-once, and both sweeps survive it without a
        // deduplication table: after the first pass the absorbed identifier answers for no
        // unsettled loan and waits in no queue.
        var absorbed = await AnEnrolledMemberAsync("20260000131");
        var surviving = await AnEnrolledMemberAsync("20260000132");

        var loanId = await ALoanOfAsync(absorbed);
        var editionId = await AQueueAsync((absorbed, 0), (surviving, 1));

        (await DispatchAsync(new MergeMembersCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        await DrainMembersAsync();
        await ReplayEveryDeliveredMessageAsync();

        var outcome = await DrainMembersAsync();

        outcome.Blockage.ShouldBeNull();
        (await BorrowerOfAsync(loanId)).ShouldBe(surviving);

        // One claim, not zero: a second pass must not read the survivor's own claims as
        // duplicates of each other.
        (await QueueOfAsync(editionId))!.Holds.ShouldHaveSingleItem()
            .PlacedOn.ShouldBe(ThisMorning);
    }

    /// <summary>
    /// Clears the processed marks, so the next drain redelivers everything the last one delivered —
    /// the crash between two transactions this mechanism promises to survive, made deterministic.
    /// </summary>
    private async Task ReplayEveryDeliveredMessageAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MembersDbContext>();

        foreach (var message in await context.Set<OutboxMessage>().ToListAsync(Token))
        {
            message.ProcessedOn = null;
        }

        await context.SaveChangesAsync(Token);
    }

    /// <summary>The clock the Members module reads today from, held still.</summary>
    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
