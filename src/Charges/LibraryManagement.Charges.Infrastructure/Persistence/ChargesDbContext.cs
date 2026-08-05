using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Charges.Infrastructure.Persistence;

/// <summary>
/// The Charges module's store.
/// </summary>
/// <param name="options">Configured by the host, which alone knows which database serves the module.</param>
/// <remarks>
/// <para>
/// A context and a schema of its own. The two identifier columns that name another module's rows —
/// the member an account belongs to, the loan a charge prices — carry no foreign key and never
/// will: no constraint crosses a schema here, and the rules those references rest on are enforced
/// where they can be explained.
/// </para>
/// <para>
/// The sets are internal, so the only way into this module's data from elsewhere is through the
/// repository the domain declared or the port Circulation asks its one question through.
/// </para>
/// </remarks>
public sealed class ChargesDbContext(DbContextOptions<ChargesDbContext> options) : DbContext(options)
{
    /// <summary>The database schema this module owns exclusively.</summary>
    public const string Schema = "charges";

    internal DbSet<MemberAccount> Accounts => Set<MemberAccount>();

    internal DbSet<Charge> Charges => Set<Charge>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChargesDbContext).Assembly);

        // This module's own outbox, in this module's own schema, for the reason every module has
        // one: the row must be written by the same save as the change that raised it.
        modelBuilder.MapOutbox();
    }
}
