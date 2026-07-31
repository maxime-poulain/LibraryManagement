using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Holdings.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Copy"/> to the store.
/// </summary>
public sealed class CopyConfiguration : AggregateRootConfiguration<Copy, CopyId>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<Copy> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Copy");

        // An identifier column, not a foreign key. It points into Catalog's schema, and no
        // constraint crosses a schema here — the rule that the edition exists is enforced where it
        // can be explained, in the handler.
        builder.Property(copy => copy.EditionId)
            .HasConversion(id => id.Value, value => EditionId.Create(value))
            .IsRequired();

        // "The copies of this edition" is the question this table is asked most, and the only one
        // it is asked constantly.
        builder.HasIndex(copy => copy.EditionId);

        builder.Property(copy => copy.Barcode)
            .HasConversion(barcode => barcode.Value, value => BarcodeOf(value))
            .HasMaxLength(Barcode.MaxLength)
            .IsRequired();

        // Unique, and this index is what actually holds the rule. The handler asks first so the
        // refusal can name the label; a constraint violation cannot say which copy already has it,
        // and this is what makes sure the answer stays true between the asking and the writing.
        builder.HasIndex(copy => copy.Barcode).IsUnique();

        builder.Property(copy => copy.Shelfmark)
            .HasConversion(shelfmark => shelfmark.Value, value => ShelfmarkOf(value))
            .HasMaxLength(Shelfmark.MaxLength)
            .IsRequired();

        // Browsing a section in shelf order is how staff read this table when they read it at all.
        builder.HasIndex(copy => copy.Shelfmark);

        // Stored as their names rather than their numbers: a status is read by a human when
        // something looks wrong, and 'InRepair' answers where '1' asks.
        builder.Property(copy => copy.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(copy => copy.Condition)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        // Null except while the copy is in repair, which is exactly what the column says.
        builder.Property(copy => copy.ReturnsTo)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(copy => copy.AcquiredOn).IsRequired();
    }

    // A stored value has already been through Create once. Re-validating on the way back would turn
    // a corrupt row into an exception no caller can act on, so materialization trusts the store and
    // any repair belongs in a migration.
    private static Barcode BarcodeOf(string value)
        => Barcode.Create(value).Match(barcode => barcode, _ => throw new InvalidOperationException(
            $"The collection holds '{value}' as a barcode, which is not a valid one."));

    private static Shelfmark ShelfmarkOf(string value)
        => Shelfmark.Create(value).Match(shelfmark => shelfmark, _ => throw new InvalidOperationException(
            $"The collection holds '{value}' as a shelfmark, which is not a valid one."));
}
