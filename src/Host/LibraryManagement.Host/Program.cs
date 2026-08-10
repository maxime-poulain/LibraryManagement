using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Migrations.SqlServer;
using LibraryManagement.Charges.Infrastructure.Extensions;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Charges.Migrations.SqlServer;
using LibraryManagement.Circulation.Infrastructure.Extensions;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Circulation.Migrations.SqlServer;
using LibraryManagement.Holdings.Infrastructure.Extensions;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Holdings.Migrations.SqlServer;
using LibraryManagement.Host.Auditing;
using LibraryManagement.Host.Http;
using LibraryManagement.Host.Jobs;
using LibraryManagement.Members.Infrastructure.Extensions;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Members.Migrations.SqlServer;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Infrastructure.Behaviors;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LibraryManagement")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:LibraryManagement is required — the host decides the database, and it "
        + "has not been told which one.");

// The one AddMediator call in this assembly, and therefore the one place its pipeline is declared.
// The source generator parses this very syntax at compile time: the behaviors must be inline typeof
// expressions, and a second call carrying a different array would leave it to pick a winner.
//
// The order is the guarantee. Logging outermost, so what validation refuses still leaves a line and
// the duration covers everything; validation before the unit of work, so a command rejected for a
// missing field never reaches the store.
//
// Scoped, not the default singleton: the outbox drain opens a scope per message, and a singleton
// mediator resolves every handler from the root scope — the handler would then write to a context
// nobody saves.
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors =
    [
        typeof(LoggingBehavior<,>),
        typeof(ValidationBehavior<,>),
        typeof(UnitOfWorkBehavior<,>),
    ];
});

// Before the modules, deliberately: AddModuleStore registers the unattributed stand-in with
// TryAdd, and first registration wins. Registered twice so the middleware and the audit interceptor
// reach the same instance — one asks for the concrete type, the other for the port.
builder.Services.AddScoped<HeaderEmployee>();
builder.Services.AddScoped<ICurrentEmployee>(services => services.GetRequiredService<HeaderEmployee>());

builder.Services
    .AddSharedInfrastructure()
    .AddCatalogModule(options => options.UseCatalogSqlServer(connectionString))
    .AddHoldingsModule(options => options.UseHoldingsSqlServer(connectionString))
    .AddMembersModule(options => options.UseMembersSqlServer(connectionString))
    .AddCirculationModule(options => options.UseCirculationSqlServer(connectionString))
    .AddChargesModule(options => options.UseChargesSqlServer(connectionString));

builder.Services.Configure<OutboxRetention>(builder.Configuration.GetSection(OutboxRetention.Section));

// Statuses and conditions travel as their names, for the reason they are stored as their names: a
// request that says "Good" is one a human can read back, and 1 is one they have to look up.
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Hangfire's storage lives in a schema of its own, beside the module schemas and owned by
// infrastructure the way sys is.
builder.Services.AddHangfire(hangfire => hangfire
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions { SchemaName = "hangfire" }));

// Whether this process also *runs* the jobs, as opposed to only enqueueing and showing them. One
// process does both today, and the switch exists because the storage is shared: the day the desk's
// traffic and the drains want separate machines, the worker is this host with the web pipeline
// idle, not a second program that would have to re-derive what the day consists of.
var runsJobs = builder.Configuration.GetValue("Hangfire:RunServer", defaultValue: true);

if (runsJobs)
{
    builder.Services.AddHangfireServer();
}

builder.Services.AddScoped<OutboxJobs>();
builder.Services.AddScoped<CirculationDailyRun>();
builder.Services.AddScoped<OutboxPurgeJob>();

var app = builder.Build();

// Migrating comes first, and the order is load-bearing: Hangfire's SQL Server storage installs its
// own schema when it is constructed, which the dashboard does at map time. A database that does not
// exist yet would meet Hangfire before it meets the modules.
var migrating = app.Services.CreateAsyncScope();
await using (migrating.ConfigureAwait(false))
{
    await migrating.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await migrating.ServiceProvider.GetRequiredService<HoldingsDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await migrating.ServiceProvider.GetRequiredService<MembersDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await migrating.ServiceProvider.GetRequiredService<CirculationDbContext>().Database.MigrateAsync().ConfigureAwait(false);
    await migrating.ServiceProvider.GetRequiredService<ChargesDbContext>().Database.MigrateAsync().ConfigureAwait(false);
}

app.UseMiddleware<EmployeeHeaderMiddleware>();

// Local requests only, because the dashboard can delete and re-enqueue jobs and nothing here
// authenticates anybody. It stays reachable from the machine that runs the host, which is where an
// operator looking at a blocked queue already is.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new LocalRequestsOnlyAuthorizationFilter()],
});

// One group per module, because a route belongs to the context that owns the act. Nothing composes
// across them yet: strategic-design §10 settles that a page is assembled at the edge from each
// module's published query, and only Catalog publishes any.
app.MapCatalog()
   .MapHoldings()
   .MapMembers()
   .MapCirculation()
   .MapCharges();

// The schedule belongs to the process that runs it. A host that only enqueues would otherwise
// rewrite the recurring definitions of a worker that is running them.
if (!runsJobs)
{
    await app.RunAsync().ConfigureAwait(false);
    return;
}

var recurring = app.Services.GetRequiredService<IRecurringJobManager>();

// One drain per module, on Hangfire's floor. Five jobs and not one, because each table blocks on
// its own head and a single job would stop four queues on one poisonous message.
recurring.AddOrUpdate<OutboxJobs>(
    OutboxJobs.CatalogJobId, jobs => jobs.DrainCatalogAsync(CancellationToken.None), Cron.Minutely);
recurring.AddOrUpdate<OutboxJobs>(
    OutboxJobs.HoldingsJobId, jobs => jobs.DrainHoldingsAsync(CancellationToken.None), Cron.Minutely);
recurring.AddOrUpdate<OutboxJobs>(
    OutboxJobs.MembersJobId, jobs => jobs.DrainMembersAsync(CancellationToken.None), Cron.Minutely);
recurring.AddOrUpdate<OutboxJobs>(
    OutboxJobs.CirculationJobId, jobs => jobs.DrainCirculationAsync(CancellationToken.None), Cron.Minutely);
recurring.AddOrUpdate<OutboxJobs>(
    OutboxJobs.ChargesJobId, jobs => jobs.DrainChargesAsync(CancellationToken.None), Cron.Minutely);

recurring.AddOrUpdate<CirculationDailyRun>(
    CirculationDailyRun.JobId, run => run.RunAsync(CancellationToken.None), Cron.Daily);
recurring.AddOrUpdate<OutboxPurgeJob>(
    OutboxPurgeJob.JobId, purge => purge.PurgeAsync(CancellationToken.None), Cron.Daily);

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Named so the integration tests can boot this host through <c>WebApplicationFactory</c>. Top-level
/// statements compile to an internal type, which that factory cannot reach.
/// </summary>
public partial class Program
{
    // The compiler puts the statements above in a generated entry point on this class; nothing
    // constructs it, and saying so is what keeps the implicit public constructor from being one
    // more thing a reader has to rule out.
    private Program()
    {
    }
}
