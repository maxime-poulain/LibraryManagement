using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// Maps the outbox table into a module's model.
/// </summary>
public static class OutboxModelBuilderExtensions
{
    /// <summary>The longest CLR type name a stored event may carry.</summary>
    public const int TypeNameLength = 512;

    /// <summary>
    /// Maps <see cref="OutboxMessage"/> into the module's own schema.
    /// </summary>
    /// <param name="modelBuilder">The model being built.</param>
    /// <returns>The same builder, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="modelBuilder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// One line in each module's <c>OnModelCreating</c>, after <c>HasDefaultSchema</c> so the table
    /// lands in the module's schema — which is the point: the row must be written by the module's
    /// own context to share its save, so each module carries its own table.
    /// </para>
    /// <para>
    /// A module that forgets this call fails loudly on the first save that raises an event:
    /// <c>Set&lt;OutboxMessage&gt;</c> throws naming the type. Loud and immediate, the same
    /// guarantee the audit mapping relies on.
    /// </para>
    /// </remarks>
    public static ModelBuilder MapOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("OutboxMessage");

            outbox.HasKey(message => message.Id);
            outbox.Property(message => message.Id).ValueGeneratedOnAdd();

            // Unique: the same occurrence stored twice would be delivered twice by design rather
            // than by accident, and the index is what keeps the accident impossible.
            outbox.HasIndex(message => message.EventId).IsUnique();

            outbox.Property(message => message.Type)
                .HasMaxLength(TypeNameLength)
                .IsRequired();

            outbox.Property(message => message.Payload).IsRequired();

            outbox.Property(message => message.OccurredOn).HasPrecision(3);
            outbox.Property(message => message.StoredOn).HasPrecision(3);
            outbox.Property(message => message.ProcessedOn).HasPrecision(3);
            outbox.Property(message => message.DeadOn).HasPrecision(3);

            // The drain's exact question — pending, in order — answered from an index that only
            // holds pending rows, so its size tracks the backlog rather than the history.
            outbox.HasIndex(message => message.Id)
                .HasFilter("[ProcessedOn] IS NULL AND [DeadOn] IS NULL")
                .HasDatabaseName("IX_OutboxMessage_Pending");
        });

        return modelBuilder;
    }
}
