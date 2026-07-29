using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Infrastructure.Search;
using LibraryManagement.Composition.Tests.Logging;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraryManagement.Composition.Tests.Outbox;

/// <summary>
/// The outbox through the whole thing: a command writes its events down inside its own save, and
/// the processor delivers them later, each in a transaction of its own.
/// </summary>
/// <remarks>
/// The processor is invoked directly rather than through a scheduler, deliberately: every behaviour
/// here is deterministic. What Hangfire adds — a clock — is proven once, in
/// <see cref="HangfireDrainTests"/>, and nowhere else.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class OutboxTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly RecordedLogs _logs = new();

    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        EchoedRegistrations.Reset();

        // The mediator, its pipeline and its scoped lifetime all come from the assembly's one
        // AddMediator call — see CompositionRoot for the reasons, this fixture included.
        _provider = CompositionRoot.Services()
            .AddLogging(logging => logging
                .AddProvider(_logs)
                .AddFilter<RecordedLogs>((category, _) =>
                    category?.StartsWith("LibraryManagement", StringComparison.Ordinal) == true))
            .AddCatalogModule(options => options.UseSqlServer(sqlServer.ConnectionString))
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.Database.EnsureDeletedAsync(Token);
        await context.Database.EnsureCreatedAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private Task<Result> DispatchAsync(string authorizedName)
        => InScopeAsync(async services => await services.GetRequiredService<ICommandDispatcher>()
            .DispatchAsync(new RegisterAuthorCommand(Guid.CreateVersion7(), authorizedName, null, null), Token));

    private Task<OutboxDrainOutcome> DrainAsync()
        => _provider.GetRequiredService<OutboxProcessor<CatalogDbContext>>().ProcessAsync(Token);

    private Task<List<OutboxMessage>> RowsAsync()
        => InScopeAsync(async services => await services.GetRequiredService<CatalogDbContext>()
            .Set<OutboxMessage>().AsNoTracking().OrderBy(message => message.Id).ToListAsync(Token));

    private async Task<Author?> FindAsync(AuthorId authorId)
        => await InScopeAsync(async services =>
            await services.GetRequiredService<IAuthorRepository>().GetByIdAsync(authorId, Token));

    // --- Writing down instead of executing -------------------------------------------------------

    [Fact]
    public async Task ACommandThatSucceeds_WritesTheEventDownInsteadOfHandlingIt()
    {
        await DispatchAsync("Ndiaye, Marie");

        var row = (await RowsAsync()).ShouldHaveSingleItem();
        row.Type.ShouldContain(nameof(AuthorRegistered));
        row.ProcessedOn.ShouldBeNull();

        // The whole point of the pattern: the fact is stored, the announcement is stored, and
        // nothing has been executed yet.
        EchoedRegistrations.Seen.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACommandThatFails_WritesNothing()
    {
        // Refused by the domain, so the unit of work never saves — and the event rides the save,
        // so there is no row. The store cannot hold the announcement without the fact.
        await InScopeAsync(async services => await services.GetRequiredService<ICommandDispatcher>()
            .DispatchAsync(new RegisterAuthorCommand(Guid.CreateVersion7(), "Nobody, Real", 1944, 1900), Token));

        (await RowsAsync()).ShouldBeEmpty();
        EchoedRegistrations.Seen.ShouldBeEmpty();
    }

    // --- Delivering ------------------------------------------------------------------------------

    [Fact]
    public async Task TheDrain_DeliversTheChain_AndMarksEachMessageWithItsOwnEffects()
    {
        // The Echo handler registers a companion, whose own registration raises a further event —
        // written as a new row by the same save that marked the first message. The chain drains
        // within one run, one message and one transaction at a time.
        await DispatchAsync(EchoedRegistrations.Echo + ", Testable");

        var outcome = await DrainAsync();

        outcome.Delivered.ShouldBe(2);
        outcome.Blockage.ShouldBeNull();
        (await RowsAsync()).ShouldAllBe(row => row.ProcessedOn != null);
        EchoedRegistrations.Seen.Count.ShouldBe(2);
        (await FindAsync(EchoedRegistrations.Companion.ShouldNotBeNull())).ShouldNotBeNull();

        // The drain says what it did — and an idle run says nothing, so this line only exists
        // because messages moved.
        _logs.Lines.ShouldContain(line =>
            line.Level == LogLevel.Information && line.Message.Contains("Outbox drained: 2"));
    }

    [Fact]
    public async Task AHandlersChanges_AreStampedLikeEveryOtherRow()
    {
        // The companion is written during the drain, by the processor's save — which runs through
        // the same interceptors as any other. An unstamped row here would mean the drain found a
        // side door into the store.
        await DispatchAsync(EchoedRegistrations.Echo + ", Stamped");

        await DrainAsync();

        var companion = await FindAsync(EchoedRegistrations.Companion.ShouldNotBeNull());
        companion.ShouldNotBeNull().CreatedOn.ShouldNotBe(default);
    }

    [Fact]
    public async Task TheSearchProjection_IsFedThroughTheDrain()
    {
        // The one consumer the strategic design names for Catalog's events, running as a real
        // production handler: before the drain the author exists but answers to nothing, after it
        // the heading is an access point. Eventual consistency, observed from the outside.
        await DispatchAsync("Ndiaye, Marie");

        (await AccessPointsAsync("Ndiaye, Marie")).ShouldBeEmpty();

        await DrainAsync();

        var accessPoint = (await AccessPointsAsync("Ndiaye, Marie")).ShouldHaveSingleItem();
        accessPoint.IsAuthorized.ShouldBeTrue();
        accessPoint.Kind.ShouldBe(AccessPointKind.Author);
    }

    [Fact]
    public async Task AnEditionsIsbn_BecomesAnAccessPointThroughTheDrain()
    {
        // End to end through the real container, deliberately: the ISBN rides the outbox as a
        // value object, so this is the one test that fails if its JSON converter is forgotten.
        var workId = Guid.CreateVersion7();
        await InScopeAsync(async services => await services.GetRequiredService<ICommandDispatcher>()
            .DispatchAsync(new RegisterWorkCommand(workId, "Le Petit Prince", []), Token));
        await InScopeAsync(async services => await services.GetRequiredService<ICommandDispatcher>()
            .DispatchAsync(new RegisterEditionCommand(Guid.CreateVersion7(), workId, "978-2-07-061275-8"), Token));

        await DrainAsync();

        var accessPoint = (await AccessPointsAsync("9782070612758")).ShouldHaveSingleItem();
        accessPoint.Kind.ShouldBe(AccessPointKind.Edition);
        accessPoint.IsAuthorized.ShouldBeTrue();
    }

    private Task<List<AccessPoint>> AccessPointsAsync(string form)
        => InScopeAsync(async services => await services.GetRequiredService<CatalogDbContext>()
            .Set<AccessPoint>().AsNoTracking()
            .Where(accessPoint => accessPoint.Form == form)
            .ToListAsync(Token));

    // --- Failing ---------------------------------------------------------------------------------

    [Fact]
    public async Task AFailingHandler_BlocksTheQueueBehindIt()
    {
        // Order is the contract: Circulation's design leans on causal order, and a queue that
        // skipped a failing message would reorder exactly when things are already going wrong.
        await DispatchAsync(EchoedRegistrations.Poison + ", First");
        await DispatchAsync("Waiting, Behind");

        var outcome = await DrainAsync();

        outcome.Delivered.ShouldBe(0);

        // The blockage names the failure so the trigger can schedule a nearer retry — attempts and
        // all, since the backoff is the trigger's decision, not this class's.
        var blockage = outcome.Blockage.ShouldNotBeNull();
        blockage.Attempts.ShouldBe(1);
        blockage.Dead.ShouldBeFalse();

        var rows = await RowsAsync();
        rows[0].Attempts.ShouldBe(1);
        rows[0].Error.ShouldNotBeNull().ShouldContain("deliberately");
        rows[0].ProcessedOn.ShouldBeNull();
        rows[1].ProcessedOn.ShouldBeNull();
        EchoedRegistrations.Seen.ShouldNotContain(name => name.Value == "Waiting, Behind");

        // The row records the failure, but nobody watches a table: the Warning line is the
        // alerting hook, and it names the event so an operator can follow it — never the payload.
        var warning = _logs.Lines.Single(line => line.Level == LogLevel.Warning);
        warning.Message.ShouldContain("blocks the queue");
        warning.Message.ShouldContain(rows[0].EventId.ToString());
        warning.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task AfterFiveAttempts_TheMessageIsDead_AndTheQueueMovesAgain()
    {
        await DispatchAsync(EchoedRegistrations.Poison + ", Forever");
        await DispatchAsync("Waiting, Behind");

        for (var attempt = 1; attempt < OutboxProcessor<CatalogDbContext>.MaxAttempts; attempt++)
        {
            (await DrainAsync()).Blockage.ShouldNotBeNull().Dead.ShouldBeFalse();
        }

        // The last allowed failure reports itself as such, so the trigger knows the head no longer
        // blocks and the queue behind it deserves a prompt follow-up rather than a backoff.
        (await DrainAsync()).Blockage.ShouldNotBeNull().Dead.ShouldBeTrue();

        var rows = await RowsAsync();
        rows[0].Attempts.ShouldBe(OutboxProcessor<CatalogDbContext>.MaxAttempts);
        rows[0].DeadOn.ShouldNotBeNull();

        // Death is an Error line where a retryable failure is only a Warning — the level is what
        // separates "the cron will handle it" from "a person must look".
        var death = _logs.Lines.Single(line => line.Level == LogLevel.Error);
        death.Message.ShouldContain("dead");
        death.Message.ShouldContain(rows[0].EventId.ToString());

        // A dead letter is an operator's problem now; the borrowers behind it are not.
        (await DrainAsync()).Delivered.ShouldBe(1);
        EchoedRegistrations.Seen.ShouldContain(name => name.Value == "Waiting, Behind");
    }
}

/// <summary>
/// Reacts to <see cref="AuthorRegistered"/>: records what it saw, and — for marked names only —
/// misbehaves on purpose.
/// </summary>
/// <remarks>
/// The markers keep it inert for every other test in this assembly. Under the outbox this handler
/// runs during the drain, in the processor's scope: what it changes is saved by the processor's own
/// <c>SaveChangesAsync</c>, together with the mark on the message that caused it.
/// </remarks>
public sealed class EchoingAuthorHandler(IAuthorRepository authors) : IDomainEventHandler<AuthorRegistered>
{
    public ValueTask Handle(AuthorRegistered notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        EchoedRegistrations.Seen.Add(notification.AuthorizedName);
        var name = notification.AuthorizedName.Value;

        if (name.StartsWith(EchoedRegistrations.Poison, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("This handler fails deliberately, for the tests.");
        }

        if (name.StartsWith(EchoedRegistrations.Echo, StringComparison.Ordinal))
        {
            // Under a name that carries no marker, so the companion's own registration — a new
            // outbox row, drained next — is answered by silence and the chain ends there.
            var companion = Author.Register(
                AuthorId.Generate(),
                PersonName.Create(EchoedRegistrations.CompanionName).Match(
                    personName => personName,
                    errors => throw new InvalidOperationException(errors[0].ToString())),
                LifeYears.Unknown);

            authors.Add(companion);
            EchoedRegistrations.Companion = companion.Id;
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// What the handler saw, read back by the test that caused it. The tests sharing this run one at a
/// time, on the collection that also serialises access to the database.
/// </summary>
public static class EchoedRegistrations
{
    /// <summary>A registration the handler answers by registering one further author.</summary>
    public const string Echo = "Echo";

    /// <summary>A registration the handler fails on, every time.</summary>
    public const string Poison = "Poison";

    /// <summary>The name the companion is registered under, carrying no marker.</summary>
    public const string CompanionName = "Written by a handler";

    public static List<PersonName> Seen { get; } = [];

    public static AuthorId? Companion { get; set; }

    public static void Reset()
    {
        Seen.Clear();
        Companion = null;
    }
}
