using Microsoft.Data.SqlClient;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// Drops a test's database before the host builds it back.
/// </summary>
/// <remarks>
/// The host migrates at startup and never drops — which is right for a host and useless for a suite
/// that wants a known starting point. So the dropping happens here, before the host boots, through
/// a plain connection rather than a context: there is no model involved in deleting a database, and
/// building one to ask for it would be borrowing a context to run one statement.
/// </remarks>
internal static class Databases
{
    public static async Task DropAsync(string connectionString, CancellationToken cancellationToken)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        var databaseName = target.InitialCatalog;
        target.InitialCatalog = "master";

        await using var connection = new SqlConnection(target.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Single-user first: Hangfire's server holds connections open, and a drop behind a live
        // session waits forever rather than failing.
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
