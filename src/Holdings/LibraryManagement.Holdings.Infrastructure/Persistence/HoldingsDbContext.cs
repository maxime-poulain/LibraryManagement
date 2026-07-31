using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Holdings.Infrastructure.Persistence;

/// <summary>
/// The Holdings module's store.
/// </summary>
/// <param name="options">Configured by the host, which alone knows which database serves the module.</param>
/// <remarks>
/// <para>
/// A context and a schema of its own, beside Catalog's and sharing nothing with it. This is the
/// first time the solution has had two, and the separation is the point: <c>Copy.EditionId</c> holds
/// an identifier that no foreign key backs, because an integrity constraint across modules is a
/// coupling the compiler cannot see. That the edition exists is asked of Catalog, in the handler,
/// where the refusal can name it.
/// </para>
/// <para>
/// The set is internal, so the only way into this module's data from elsewhere is through a
/// repository the domain declared or the published language this module offers.
/// </para>
/// </remarks>
public sealed class HoldingsDbContext(DbContextOptions<HoldingsDbContext> options) : DbContext(options)
{
    /// <summary>The database schema this module owns exclusively.</summary>
    public const string Schema = "holdings";

    internal DbSet<Copy> Copies => Set<Copy>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HoldingsDbContext).Assembly);

        // This module's own outbox, in this module's own schema — the second one in the solution.
        // A central table would be a second context and a second transaction, and the row must be
        // written by the same save as the change that raised it.
        modelBuilder.MapOutbox();
    }
}
