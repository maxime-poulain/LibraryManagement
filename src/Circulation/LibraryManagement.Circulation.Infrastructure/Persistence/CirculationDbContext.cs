using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Infrastructure.Persistence;

/// <summary>
/// The Circulation module's store.
/// </summary>
/// <param name="options">Configured by the host, which alone knows which database serves the module.</param>
/// <remarks>
/// <para>
/// A context and a schema of its own, beside the three it holds identifiers into — and only
/// identifiers: no foreign key in this schema points anywhere, because an integrity constraint
/// across modules is a coupling the compiler cannot see. That a copy exists is Holdings'
/// question, that a person is entitled is Members', and both are asked where the refusal can be
/// explained, in the handlers.
/// </para>
/// <para>
/// The sets are internal, so the only way into this module's data from elsewhere is through a
/// repository the domain declared.
/// </para>
/// </remarks>
public sealed class CirculationDbContext(DbContextOptions<CirculationDbContext> options)
    : DbContext(options)
{
    /// <summary>The database schema this module owns exclusively.</summary>
    public const string Schema = "circulation";

    internal DbSet<Loan> Loans => Set<Loan>();

    internal DbSet<HoldQueue> HoldQueues => Set<HoldQueue>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CirculationDbContext).Assembly);

        // This module's own outbox, in this module's own schema. A central table would be a second
        // context and a second transaction, and the row must be written by the same save as the
        // change that raised it.
        modelBuilder.MapOutbox();
    }
}
