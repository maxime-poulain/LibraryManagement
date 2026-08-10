using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// A real SQL Server, shared by every test in this assembly.
/// </summary>
/// <remarks>
/// The third copy of this class in the repository, and deliberately a copy rather than something
/// shared: a test assembly that took a dependency on another test assembly to get its fixture would
/// couple two suites through their scaffolding. Each owns a database named after itself, and the
/// duplication is the price of that independence.
/// </remarks>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// Names a SQL Server to run the integration tests against, instead of starting a container.
    /// </summary>
    public const string ConnectionStringVariable = "LIBRARYMANAGEMENT_TEST_SQLSERVER";

    private const string DatabaseName = "LibraryManagement_Host_Tests";

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

    /// <summary>The database the host under test is pointed at.</summary>
    public string ConnectionString => _connectionString;

    public async ValueTask InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
            _connectionString = WithOurOwnDatabase(_container.GetConnectionString());
        }
    }

    // A database of this suite's own, whichever server it is. A container hands back a connection
    // string pointing at master, and dropping master is not something to ask of any server.
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
/// <remarks>
/// Every class joining this collection also carries <c>[Trait("Category", "Integration")]</c> — the
/// pair travels together, one attribute for xUnit and one for the runner that decides what a
/// pull-request build skips.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sql server";
}
