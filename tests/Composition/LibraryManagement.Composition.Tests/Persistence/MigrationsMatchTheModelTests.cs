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
using LibraryManagement.Members.Infrastructure.Extensions;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Members.Migrations.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.Persistence;

/// <summary>
/// Every module's migrations describe the model the module actually maps.
/// </summary>
/// <remarks>
/// <para>
/// The cost <c>migrations.md</c> named while the suite still built its schema from the model: the
/// tests validated a schema built by a different mechanism than a deployment would use, so a
/// migration that had drifted from the model would have passed every test in the repository. The
/// fixtures now migrate, which closes most of that gap — a migration that fails to apply fails
/// loudly. This closes the rest: a model changed without a migration applies cleanly and is simply
/// missing a column, and only asking the question catches it.
/// </para>
/// <para>
/// The answer is a compile-time comparison of the model against the last migration's snapshot, so
/// it needs no database — but the options must be the ones the host builds, migrations assembly
/// included, or the question is asked of a model configured differently from the one that ships.
/// </para>
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MigrationsMatchTheModelTests(SqlServerFixture sqlServer)
{
    // Never connected to: HasPendingModelChanges compares the model in memory against the snapshot
    // the tooling wrote. The fixture is here so the string is a real one rather than an invention.
    private string ConnectionString => new SqlConnectionStringBuilder(sqlServer.ConnectionString)
    {
        InitialCatalog = "LibraryManagement_MigrationsMatchTheModel_Tests",
    }.ConnectionString;

    [Fact]
    public void Catalog_HasNoModelChangeItsMigrationsDoNotDescribe()
        => ShouldHaveNothingPending<CatalogDbContext>(
            services => services.AddCatalogModule(
                options => options.UseCatalogSqlServer(ConnectionString)));

    [Fact]
    public void Holdings_HasNoModelChangeItsMigrationsDoNotDescribe()
        => ShouldHaveNothingPending<HoldingsDbContext>(
            services => services.AddHoldingsModule(
                options => options.UseHoldingsSqlServer(ConnectionString)));

    [Fact]
    public void Members_HasNoModelChangeItsMigrationsDoNotDescribe()
        => ShouldHaveNothingPending<MembersDbContext>(
            services => services.AddMembersModule(
                options => options.UseMembersSqlServer(ConnectionString)));

    [Fact]
    public void Circulation_HasNoModelChangeItsMigrationsDoNotDescribe()
        => ShouldHaveNothingPending<CirculationDbContext>(
            services => services.AddCirculationModule(
                options => options.UseCirculationSqlServer(ConnectionString)));

    [Fact]
    public void Charges_HasNoModelChangeItsMigrationsDoNotDescribe()
        => ShouldHaveNothingPending<ChargesDbContext>(
            services => services.AddChargesModule(
                options => options.UseChargesSqlServer(ConnectionString)));

    private static void ShouldHaveNothingPending<TContext>(Action<IServiceCollection> register)
        where TContext : DbContext
    {
        var services = CompositionRoot.Services();
        register(services);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<TContext>().Database
            .HasPendingModelChanges()
            .ShouldBeFalse(
                $"{typeof(TContext).Name} maps something its migrations do not describe — "
                + "add a migration for the change.");
    }
}
