using LibraryManagement.Members.Application.Members.EnrollMember;
using LibraryManagement.Members.Application.Members.MergeMembers;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.Extensions;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Members.Migrations.SqlServer;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
// One module, two types of the same name: the domain event the aggregate raises and the flat record
// the translator produces. A file touching both is forced to say which it means, which is exactly
// the distinction the translation exists to make.
using Contract = LibraryManagement.Members.PublishedLanguage.MembersMerged;

namespace LibraryManagement.Composition.Tests.IntegrationEvents;

/// <summary>
/// The first fact Members states on its own initiative, travelling as far as it currently goes:
/// through the aggregate, the outbox row, the drain and the translation into a flat contract.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Nobody acts on it, and that is what this file records.</strong> Circulation and Charges
/// are the consumers ADR-0017 names and neither is built, so the announcement ends at a subscriber
/// this test registers to observe it. Writing the consumers first would have meant writing them
/// against an intention; announcing first is what let each edition consumer be written against a
/// fact that already existed.
/// </para>
/// <para>
/// A stand-in subscriber rather than a real one is legitimate here for the same reason it is
/// nowhere else in this folder: there is no production subscriber to displace, so nothing can
/// silently win by registration order.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MembersMergedTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly Announcements _heard = new();

    private ServiceProvider _provider = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_MembersMerged_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        _provider = CompositionRoot.Services()
            .AddSingleton<TimeProvider>(new FrozenClock(
                new DateTimeOffset(Today, TimeOnly.MinValue, TimeSpan.Zero)))
            .AddMembersModule(options => options.UseMembersSqlServer(ConnectionString))
            .AddSingleton(_heard)
            .AddScoped<IIntegrationEventSubscriber<Contract>, WhoeverIsListening>()
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MembersDbContext>();
        await context.Database.EnsureDeletedAsync(Token);
        await context.Database.MigrateAsync(Token);
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
                Email: "antoine.doinel@example.org",
                Phone: null,
                PostalAddress: null,
                Guardian: null)))
            .HasErrors().ShouldBeFalse();

        return memberId;
    }

    [Fact]
    public async Task AMergeAtTheDesk_LeavesTheLibraryWithOneFlatFact()
    {
        var absorbed = await AnEnrolledMemberAsync("20260000001");
        var surviving = await AnEnrolledMemberAsync("20260000002");

        (await DispatchAsync(new MergeMembersCommand(absorbed, surviving)))
            .HasErrors().ShouldBeFalse();

        _heard.Contracts.ShouldBeEmpty("nothing has been drained yet");

        var outcome = await DrainMembersAsync();

        outcome.Blockage.ShouldBeNull();

        var announced = _heard.Contracts.ShouldHaveSingleItem();
        announced.AbsorbedMemberId.ShouldBe(absorbed);
        announced.SurvivingMemberId.ShouldBe(surviving);

        // Two identifiers and nothing else: a contract carrying a name would put a person's name
        // into every schema downstream, which is exactly what this boundary bought by refusing it.
        announced.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task TheAbsorbedRecordKeepsItsFields_AndPointsAtTheSurvivor()
    {
        var absorbed = await AnEnrolledMemberAsync("20260000011");
        var surviving = await AnEnrolledMemberAsync("20260000012");

        await DispatchAsync(new MergeMembersCommand(absorbed, surviving));

        await using var scope = _provider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<MembersDbContext>()
            .Set<Member>()
            .SingleAsync(member => member.Id == MemberId.Create(absorbed), Token);

        stored.MergedInto.ShouldBe(MemberId.Create(surviving));
        stored.Name.ShouldNotBeNull();
        stored.CardNumber.ShouldNotBeNull();
    }

    /// <summary>The clock the module reads today from, held still.</summary>
    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Whatever the day's composition happens to have listening.</summary>
    internal sealed class Announcements
    {
        public List<Contract> Contracts { get; } = [];
    }

    private sealed class WhoeverIsListening(Announcements heard)
        : IIntegrationEventSubscriber<Contract>
    {
        public ValueTask HandleAsync(
            Contract contract,
            CancellationToken cancellationToken = default)
        {
            heard.Contracts.Add(contract);
            return ValueTask.CompletedTask;
        }
    }
}
