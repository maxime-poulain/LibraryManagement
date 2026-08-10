using LibraryManagement.Circulation.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using LibraryManagement.Circulation.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.MsSql;

namespace LibraryManagement.Circulation.Infrastructure.Tests;

/// <summary>
/// A real SQL Server, shared by every integration test in this assembly.
/// </summary>
/// <remarks>
/// The engine under test is the engine the modules target. This module leans on it harder than
/// most: two filtered unique indexes — one active loan per copy, one hold per trapped copy — are
/// what hold the rules the handlers only report on, and a stand-in engine would prove neither.
/// Set <see cref="ConnectionStringVariable"/> to use a server of the environment's choosing;
/// leave it unset and a container is started.
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// Names a SQL Server to run the integration tests against, instead of starting a container.
    /// </summary>
    public const string ConnectionStringVariable = "LIBRARYMANAGEMENT_TEST_SQLSERVER";

    private const string DatabaseName = "LibraryManagement_Circulation_Tests";

    private readonly MsSqlContainer? _container;
    private string _connectionString;

    public SqlServerFixture()
    {
        var provided = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        if (string.IsNullOrWhiteSpace(provided))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            _connectionString = string.Empty;
        }
        else
        {
            _connectionString = WithOurOwnDatabase(provided);
        }
    }

    public async ValueTask InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
            _connectionString = WithOurOwnDatabase(_container.GetConnectionString());
        }

        // Dropped and rebuilt, so a run never inherits the schema of an older model. Built by the
        // migrations rather than from the model: the suite tests the schema a deployment would
        // get, and a migration that drifted from the model fails here.
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    // A database of this suite's own, whichever server it is: a container hands back a connection
    // string pointing at master, and dropping master is not something to ask of any server.
    private static string WithOurOwnDatabase(string connectionString)
        => new SqlConnectionStringBuilder(connectionString) { InitialCatalog = DatabaseName }
            .ConnectionString;

    /// <summary>
    /// Opens a context of its own. Reading back through a second one is what proves a value
    /// reached the database rather than merely the change tracker.
    /// </summary>
    public CirculationDbContext NewContext(params IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<CirculationDbContext>()
            .UseCirculationSqlServer(_connectionString)
            .AddInterceptors(interceptors)
            .Options);

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

/// <summary>
/// Binds the fixture to the tests that share it, so the server is prepared once per run.
/// </summary>
/// <remarks>
/// Every class joining this collection also carries <c>[Trait("Category", "Integration")]</c>, and
/// that trait is what the pull-request workflow excludes: a run on a hosted agent compiles and
/// tests without ever starting a container. The two attributes say the same thing to two
/// audiences — one to xUnit, which hands out the fixture, one to the runner, which decides what
/// to skip — and needing the fixture is what makes a test an integration test, so the pair
/// travels together.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql server";
}
