using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Migrations.SqlServer;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.Outbox;

/// <summary>
/// The purge, against a real store: what it removes, what it must not, and the window that decides.
/// </summary>
/// <remarks>
/// A real database rather than a double, because the deletes are statements the store executes —
/// nothing is materialized, so an in-memory stand-in would exercise a different mechanism than the
/// one that runs. The rows are written directly: what matters here is the shape of the table, never
/// how a row came to be in it.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class OutboxPurgeTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    // Its own database: this class writes rows straight into the outbox table and counts what
    // survives, which no class sharing a database with it could tolerate.
    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_OutboxPurge_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        _provider = CompositionRoot.Services()
            .AddSingleton<TimeProvider>(new FrozenClock(Now))
            .AddCatalogModule(options => options.UseCatalogSqlServer(ConnectionString))
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.Database.EnsureDeletedAsync(Token);
        await context.Database.MigrateAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    private async Task WriteAsync(params OutboxMessage[] messages)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        context.Set<OutboxMessage>().AddRange(messages);
        await context.SaveChangesAsync(Token);
    }

    private static OutboxMessage AMessage(
        DateTimeOffset? processedOn = null,
        DateTimeOffset? deadOn = null)
        => new()
        {
            EventId = Guid.CreateVersion7(),
            Type = "LibraryManagement.Catalog.Domain.Authors.AuthorRegistered, LibraryManagement.Catalog.Domain",
            Payload = "{}",
            OccurredOn = Now.AddDays(-90),
            StoredOn = Now.AddDays(-90),
            ProcessedOn = processedOn,
            DeadOn = deadOn,
        };

    private Task<OutboxPurgeOutcome> PurgeAsync()
        => _provider.GetRequiredService<OutboxPurge<CatalogDbContext>>().PurgeAsync(Retention, Token);

    private async Task<List<OutboxMessage>> RowsAsync()
    {
        await using var scope = _provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .Set<OutboxMessage>()
            .OrderBy(message => message.Id)
            .ToListAsync(Token);
    }

    [Fact]
    public async Task ADeliveredMessage_PastTheWindow_IsRemoved()
    {
        await WriteAsync(AMessage(processedOn: Now - Retention.Add(TimeSpan.FromDays(1))));

        (await PurgeAsync()).Delivered.ShouldBe(1);

        (await RowsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ADeliveredMessage_InsideTheWindow_IsKept()
    {
        // The window is a retention period, not a broom: what it holds is what an operator can still
        // read back when asked what was delivered and when.
        await WriteAsync(AMessage(processedOn: Now - Retention.Subtract(TimeSpan.FromDays(1))));

        (await PurgeAsync()).Delivered.ShouldBe(0);

        (await RowsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ADeadMessage_PastTheWindow_IsRemovedAndCountedApart()
    {
        // The harder half of the policy. A dead letter stays for an operator, and "for an operator"
        // cannot mean "forever" — the window is their response time, and its end is announced.
        await WriteAsync(AMessage(deadOn: Now - Retention.Add(TimeSpan.FromDays(1))));

        var outcome = await PurgeAsync();

        outcome.Dead.ShouldBe(1);
        outcome.Delivered.ShouldBe(0, "a message nobody could deliver was never delivered");
        (await RowsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ADeadMessage_InsideTheWindow_IsKept()
    {
        await WriteAsync(AMessage(deadOn: Now - Retention.Subtract(TimeSpan.FromDays(1))));

        (await PurgeAsync()).Dead.ShouldBe(0);

        (await RowsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task APendingMessage_IsNeverRemoved_HoweverOldItIs()
    {
        // The one row the purge must never touch: undelivered, and old precisely because the queue
        // ahead of it is blocked. Deleting it would lose a fact nobody has read yet.
        await WriteAsync(AMessage());

        var outcome = await PurgeAsync();

        outcome.Delivered.ShouldBe(0);
        outcome.Dead.ShouldBe(0);
        (await RowsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ANegativeWindow_IsRefused()
    {
        // It would put the cutoff in the future, and the pending rows the drain has not reached yet
        // are the ones that would go.
        await Should.ThrowAsync<ArgumentOutOfRangeException>(
            () => _provider.GetRequiredService<OutboxPurge<CatalogDbContext>>()
                .PurgeAsync(TimeSpan.FromDays(-1), Token));
    }

    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
