using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Persistence;

/// <summary>
/// The Catalog module's store.
/// </summary>
/// <param name="options">Configured by the host, which alone knows which database serves the module.</param>
/// <remarks>
/// <para>
/// One context per module, on a schema of its own. A single context spanning every module would
/// compile, and one <c>DbSet&lt;Copy&gt;</c> reached from a Catalog handler would end the separation
/// without anything failing. No foreign key leaves <see cref="Schema"/>: a module referencing
/// another's row holds an identifier, and the database does not enforce that reference — an
/// integrity constraint across modules is a coupling the compiler cannot see.
/// </para>
/// <para>
/// The sets are internal. Nothing outside this assembly can reach them, so the only way into the
/// catalogue's data from elsewhere is through a repository or a query abstraction the domain
/// declared.
/// </para>
/// </remarks>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    /// <summary>The database schema this module owns exclusively.</summary>
    public const string Schema = "catalog";

    internal DbSet<Author> Authors => Set<Author>();

    internal DbSet<Work> Works => Set<Work>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

        // The module's own outbox, in the module's own schema, because an event's row must be
        // written by the same save as the change that raised it — same context, same transaction.
        modelBuilder.MapOutbox();
    }
}
