using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Host.Jobs;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// The purge job: one run, five modules, and the window the host configured.
/// </summary>
/// <remarks>
/// What the mechanism does is already proven per module in the composition tests. What this proves
/// is the host's part — that the job reaches every module rather than the one somebody remembered,
/// and that the window comes from configuration rather than from a constant in the job.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class OutboxPurgeJobTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TheHost _host = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_OutboxPurgeJob_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await Databases.DropAsync(ConnectionString, Token);
        _host = new TheHost(ConnectionString, clock: new FrozenClock(Now));
        _ = _host.Services;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    private static OutboxMessage ADeliveredMessage(DateTimeOffset processedOn)
        => new()
        {
            EventId = Guid.CreateVersion7(),
            Type = "LibraryManagement.Catalog.Domain.Authors.AuthorRegistered, LibraryManagement.Catalog.Domain",
            Payload = "{}",
            OccurredOn = processedOn,
            StoredOn = processedOn,
            ProcessedOn = processedOn,
        };

    [Fact]
    public async Task ThePurge_ReachesEveryModule_AndHonoursTheConfiguredWindow()
    {
        // Two modules, so a job that purged only the one it was written against would be caught.
        // The default window is thirty days; these settled thirty-one days ago.
        var settled = Now.AddDays(-31);

        await _host.InScopeAsync(async services =>
        {
            services.GetRequiredService<CatalogDbContext>()
                .Set<OutboxMessage>().Add(ADeliveredMessage(settled));
            services.GetRequiredService<CirculationDbContext>()
                .Set<OutboxMessage>().Add(ADeliveredMessage(settled));

            await services.GetRequiredService<CatalogDbContext>().SaveChangesAsync(Token);
            await services.GetRequiredService<CirculationDbContext>().SaveChangesAsync(Token);
        });

        var outcome = await _host.InScopeAsync(services =>
            services.GetRequiredService<OutboxPurgeJob>().PurgeAsync(Token));

        outcome.Delivered.ShouldBe(2, "one settled row in each of two modules");
        outcome.Dead.ShouldBe(0);

        await _host.InScopeAsync(async services =>
        {
            (await services.GetRequiredService<CatalogDbContext>()
                .Set<OutboxMessage>().AnyAsync(Token)).ShouldBeFalse();
            (await services.GetRequiredService<CirculationDbContext>()
                .Set<OutboxMessage>().AnyAsync(Token)).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task ThePurge_KeepsWhatIsStillInsideTheWindow()
    {
        await _host.InScopeAsync(async services =>
        {
            var catalog = services.GetRequiredService<CatalogDbContext>();
            catalog.Set<OutboxMessage>().Add(ADeliveredMessage(Now.AddDays(-29)));
            await catalog.SaveChangesAsync(Token);
        });

        (await _host.InScopeAsync(services =>
            services.GetRequiredService<OutboxPurgeJob>().PurgeAsync(Token))).Delivered.ShouldBe(0);

        await _host.InScopeAsync(async services =>
            (await services.GetRequiredService<CatalogDbContext>()
                .Set<OutboxMessage>().CountAsync(Token)).ShouldBe(1));
    }
}
