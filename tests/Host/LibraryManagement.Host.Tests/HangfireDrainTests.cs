using Hangfire;
using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using LibraryManagement.Host.Jobs;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// The one test that runs a drain through Hangfire itself: the host's own storage, its own server,
/// its own activation through the container.
/// </summary>
/// <remarks>
/// Everything else about the outbox is deterministic and tested by invoking the processor directly,
/// in the composition tests. What this proves is only the clockwork — and now proves it against the
/// composition that actually ships, rather than one assembled for the occasion.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class HangfireDrainTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private TheHost _host = null!;

    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_HangfireDrain_Tests",
    }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        await Databases.DropAsync(ConnectionString, Token);

        // The only test that lets the server tick, because it is the only one asking whether it
        // does. Booting is already the assertion that the composition holds.
        _host = new TheHost(ConnectionString, runsJobs: true);
        _ = _host.Services;
    }

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task TheDrain_RunsThroughHangfire()
    {
        await _host.InScopeAsync(async services =>
        {
            var dispatched = await services.GetRequiredService<ICommandDispatcher>()
                .DispatchAsync(
                    new RegisterAuthorCommand(Guid.CreateVersion7(), "Ernaux, Annie", null, null),
                    Token);

            dispatched.HasErrors().ShouldBeFalse();
        });

        // Enqueued rather than waited for: the recurring drain runs on Hangfire's floor of one
        // minute, and what this test is about is the path — storage, server, activation — not the
        // schedule that walks it.
        _host.Services.GetRequiredService<IBackgroundJobClient>()
            .Enqueue<OutboxJobs>(jobs => jobs.DrainCatalogAsync(CancellationToken.None));

        // The table is the honest observable: a message the drain delivered is a message the drain
        // marked, in the same save as whatever the handler did.
        (await WaitUntilDrainedAsync()).ShouldBeTrue(
            "the enqueued job should have drained Catalog's outbox");
    }

    private async Task<bool> WaitUntilDrainedAsync()
    {
        // Generous because a cold Hangfire storage installs its schema on the way through.
        for (var attempt = 0; attempt < 120; attempt++)
        {
            var pending = await _host.InScopeAsync(services => services
                .GetRequiredService<CatalogDbContext>()
                .Set<OutboxMessage>()
                .AnyAsync(message => message.ProcessedOn == null, Token));

            if (!pending)
            {
                return true;
            }

            await Task.Delay(250, Token);
        }

        return false;
    }
}
