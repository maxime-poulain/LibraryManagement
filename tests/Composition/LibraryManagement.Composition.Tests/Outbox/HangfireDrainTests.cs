using Hangfire;
using Hangfire.SqlServer;
using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Holdings.Infrastructure.Extensions;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.Outbox;

/// <summary>
/// The one test that runs the drain through Hangfire itself: storage, server, activation through
/// the container. Everything else about the outbox is deterministic and tested by invoking the
/// processor directly — what this proves is only the clockwork.
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class HangfireDrainTests(SqlServerFixture sqlServer) : IAsyncLifetime
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider _provider = null!;

    public async ValueTask InitializeAsync()
    {
        EchoedRegistrations.Reset();

        // The mediator, its pipeline and its scoped lifetime come from the assembly's one
        // AddMediator call — see CompositionRoot.
        // Both modules, because OutboxJobs is the host's surface and takes one processor per module
        // it hosts. That is deliberate: a job per module is what lets each table block on its own
        // head, and a single job looping over the modules would let one poisonous message stop every
        // other queue. The cost is this line — a host that wires a module wires its drain with it.
        _provider = CompositionRoot.Services()
            .AddCatalogModule(options => options.UseSqlServer(sqlServer.ConnectionString))
            .AddHoldingsModule(options => options.UseSqlServer(sqlServer.ConnectionString))
            .AddTransient<OutboxJobs>()
            .BuildServiceProvider();

        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await context.Database.EnsureDeletedAsync(Token);
        await context.Database.EnsureCreatedAsync(Token);
    }

    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();

    [Fact]
    public async Task TheDrain_RunsThroughHangfire()
    {
        await using (var scope = _provider.CreateAsyncScope())
        {
            var dispatched = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
                .DispatchAsync(new RegisterAuthorCommand(Guid.CreateVersion7(), "Ernaux, Annie", null, null), Token);
            dispatched.Match(() => true, _ => false).ShouldBeTrue();
        }

        // Hangfire's schema lives beside the module schemas, owned by infrastructure the way sys
        // is. The storage installs it on construction.
        var storage = new SqlServerStorage(sqlServer.ConnectionString, new SqlServerStorageOptions
        {
            SchemaName = "hangfire",
            QueuePollInterval = TimeSpan.FromMilliseconds(250),
        });

        new BackgroundJobClient(storage)
            .Enqueue<OutboxJobs>(jobs => jobs.DrainCatalogAsync(CancellationToken.None));

        using var server = new BackgroundJobServer(
            new BackgroundJobServerOptions
            {
                Activator = new ScopedJobActivator(_provider.GetRequiredService<IServiceScopeFactory>()),
                WorkerCount = 1,
            },
            storage);

        await WaitUntilDrainedAsync();

        EchoedRegistrations.Seen.ShouldContain(name => name.Value == "Ernaux, Annie");
    }

    private async Task WaitUntilDrainedAsync()
    {
        // Generous because a cold Hangfire storage initialises itself on the way; the assertion
        // after the loop is what fails, loudly, if the job never ran.
        for (var i = 0; i < 120; i++)
        {
            await using var scope = _provider.CreateAsyncScope();
            var pending = await scope.ServiceProvider.GetRequiredService<CatalogDbContext>()
                .Set<OutboxMessage>()
                .AnyAsync(message => message.ProcessedOn == null, Token);

            if (!pending)
            {
                return;
            }

            await Task.Delay(250, Token);
        }
    }

    // Hangfire.Core's own extension point, implemented over the container so a job's dependencies
    // — the processor, its scope factory — come from the same registrations everything else uses.
    private sealed class ScopedJobActivator(IServiceScopeFactory scopes) : JobActivator
    {
        public override JobActivatorScope BeginScope(JobActivatorContext context)
            => new Scope(scopes.CreateScope());

        private sealed class Scope(IServiceScope scope) : JobActivatorScope
        {
            public override object Resolve(Type type)
                => scope.ServiceProvider.GetRequiredService(type);

            public override void DisposeScope() => scope.Dispose();
        }
    }
}
