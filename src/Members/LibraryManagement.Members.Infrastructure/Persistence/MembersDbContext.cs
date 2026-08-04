using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Members.Infrastructure.Persistence;

/// <summary>
/// The Members module's store.
/// </summary>
/// <param name="options">Configured by the host, which alone knows which database serves the module.</param>
/// <remarks>
/// <para>
/// A context and a schema of its own, beside Catalog's and Holdings', sharing nothing with
/// either. Nothing in this schema points anywhere: Members is upstream of everything it touches,
/// so there is not even an identifier column here that names another module's row — the
/// references run the other way, and it is Circulation that will hold this module's identifier
/// under its own <c>BorrowerId</c>.
/// </para>
/// <para>
/// The set is internal, so the only way into this module's data from elsewhere is through a
/// repository the domain declared or the published language this module offers.
/// </para>
/// </remarks>
public sealed class MembersDbContext(DbContextOptions<MembersDbContext> options) : DbContext(options)
{
    /// <summary>The database schema this module owns exclusively.</summary>
    public const string Schema = "members";

    internal DbSet<Member> Members => Set<Member>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MembersDbContext).Assembly);

        // This module's own outbox, in this module's own schema. A central table would be a second
        // context and a second transaction, and the row must be written by the same save as the
        // change that raised it.
        modelBuilder.MapOutbox();
    }
}
