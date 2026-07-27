using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace LibraryManagement.Composition.Tests;

/// <summary>
/// A real SQL Server for the tests that drive the whole pipeline.
/// </summary>
/// <remarks>
/// Deliberately a copy of the one in the Catalog infrastructure tests rather than a shared type: a
/// project referenced by both would exist only to hold it, and the contract that matters — the
/// environment variable — is the same in either place.
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// Names a SQL Server to run against, instead of starting a container.
    /// </summary>
    public const string ConnectionStringVariable = "LIBRARYMANAGEMENT_TEST_SQLSERVER";

    private const string DatabaseName = "LibraryManagement_Pipeline_Tests";

    private readonly MsSqlContainer? _container;

    public SqlServerFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        }
        else
        {
            ConnectionString = WithOurOwnDatabase(ConnectionString);
        }
    }

    public string ConnectionString { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
            ConnectionString = WithOurOwnDatabase(_container.GetConnectionString());
        }
    }

    // A database of this suite's own, whichever server it is. A container hands back a connection
    // string pointing at master, and dropping and recreating master is not something SQL Server
    // will do — nor something a suite should ask of a server it did not start.
    private static string WithOurOwnDatabase(string connectionString)
        => new SqlConnectionStringBuilder(connectionString) { InitialCatalog = DatabaseName }
            .ConnectionString;

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
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql server";
}
