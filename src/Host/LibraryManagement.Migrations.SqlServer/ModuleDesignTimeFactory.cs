using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LibraryManagement.Migrations.SqlServer;

/// <summary>
/// Builds a module's context for the migrations tooling.
/// </summary>
/// <typeparam name="TContext">The context this factory produces.</typeparam>
/// <remarks>
/// What lets <c>dotnet ef migrations add</c> work without a host: the tooling cannot construct a
/// context whose options come from somebody's startup, so a factory hands it one. The connection
/// string only has to parse — adding a migration compares the model against the previous migration
/// and never opens a connection.
/// </remarks>
public abstract class ModuleDesignTimeFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    /// <summary>
    /// A connection string that never connects, shared by every module's factory.
    /// </summary>
    protected const string DesignTimeOnly =
        "Server=design-time-only;Database=LibraryManagement;Trusted_Connection=True;TrustServerCertificate=True";

    /// <inheritdoc/>
    public TContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TContext>();
        Configure(options);

        return Build(options.Options);
    }

    /// <summary>
    /// Points the builder at this module's schema and migrations, through the module's own named
    /// extension.
    /// </summary>
    /// <param name="options">The builder to configure.</param>
    protected abstract void Configure(DbContextOptionsBuilder<TContext> options);

    /// <summary>
    /// Constructs the context. A constructor cannot be called through a generic parameter with the
    /// argument it needs, so each module says this in one line.
    /// </summary>
    /// <param name="options">The configured options.</param>
    /// <returns>The context the tooling asked for.</returns>
    protected abstract TContext Build(DbContextOptions<TContext> options);
}
