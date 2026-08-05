using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Circulation.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="HoldQueue"/> to the store.
/// </summary>
/// <remarks>
/// The queue row itself carries nothing but its identity and the audit trail — the aggregate is
/// its holds, in a table of their own, because a hold is looked up by borrower on every cap count
/// and that search wants an index a JSON column cannot give.
/// </remarks>
public sealed class HoldQueueConfiguration : AggregateRootConfiguration<HoldQueue, EditionId>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<HoldQueue> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("HoldQueue");

        builder.OwnsMany(queue => queue.Holds, hold =>
        {
            hold.ToTable("Hold");
            hold.WithOwner().HasForeignKey("EditionId");

            hold.Property(h => h.Id)
                .HasConversion(id => id.Value, value => HoldId.Create(value))
                .IsRequired();

            hold.HasKey("EditionId", "Id");

            hold.Property(h => h.BorrowerId)
                .HasConversion(id => id.Value, value => BorrowerId.Create(value))
                .IsRequired();

            // The other half of every cap count: a borrower's live holds, across all queues.
            hold.HasIndex(h => h.BorrowerId);

            hold.Property(h => h.PlacedOn).IsRequired();

            hold.Property(h => h.Status)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();

            hold.Property(h => h.TrappedCopyId)
                .HasConversion(id => id!.Value, value => CopyId.Create(value));

            // A trapped copy belongs to exactly one hold — in any queue, not just its own. The
            // aggregate holds the rule within a queue; this filtered index holds it across them.
            hold.HasIndex(h => h.TrappedCopyId)
                .IsUnique()
                .HasFilter("[TrappedCopyId] IS NOT NULL");

            hold.Property(h => h.PickupDeadline);
        });

        builder.Navigation(queue => queue.Holds)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
