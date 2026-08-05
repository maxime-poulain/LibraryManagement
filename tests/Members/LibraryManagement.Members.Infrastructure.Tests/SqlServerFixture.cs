using LibraryManagement.Members.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Testcontainers.MsSql;

namespace LibraryManagement.Members.Infrastructure.Tests;

/// <summary>
/// A real SQL Server, shared by every integration test in this assembly.
/// </summary>
/// <remarks>
/// <para>
/// The engine under test is the engine the modules target. A lighter stand-in would prove the
/// mapping works somewhere the system will never run, and would miss what this module rests on: a
/// schema of its own, a <c>rowversion</c> the engine maintains, and a member's values spread over
/// the row's own columns.
/// </para>
/// <para>
/// Where that engine comes from is an environment concern, not a test one. Set
/// <see cref="ConnectionStringVariable"/> and the tests use the server it names — a build agent's
/// service container, a local instance. Leave it unset and a container is started, so a fresh
/// clone needs no setup beyond Docker.
/// </para>
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// Names a SQL Server to run the integration tests against, instead of starting a container.
    /// </summary>
    public const string ConnectionStringVariable = "LIBRARYMANAGEMENT_TEST_SQLSERVER";

    private const string DatabaseName = "LibraryManagement_Members_Tests";

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

        // Dropped and rebuilt, so a run never inherits the schema of an older model.
        await using var context = NewContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    // A database of this suite's own, whichever server it is. A container hands back a connection
    // string pointing at master, and dropping and recreating master is not something SQL Server
    // will do — nor something a suite should ask of a server it did not start.
    private static string WithOurOwnDatabase(string connectionString)
        => new SqlConnectionStringBuilder(connectionString) { InitialCatalog = DatabaseName }
            .ConnectionString;

    /// <summary>
    /// Opens a context of its own. Reading back through a second one is what proves a value
    /// reached the database rather than merely the change tracker.
    /// </summary>
    public MembersDbContext NewContext(params IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<MembersDbContext>()
            .UseSqlServer(_connectionString)
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
///
/// The default is deliberately the safe one. An unmarked test runs everywhere, so a forgotten
/// trait costs a slow pull-request build and a loud failure; it can never quietly remove a test
/// from the gate that protects the branch.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql server";
}
