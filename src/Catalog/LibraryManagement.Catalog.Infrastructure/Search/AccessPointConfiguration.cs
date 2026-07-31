using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Catalog.Infrastructure.Search;

/// <summary>
/// Maps <see cref="AccessPoint"/> to the store.
/// </summary>
/// <remarks>
/// Implements the configuration interface directly rather than the aggregate base, because this is
/// not an aggregate: no identifier of its own, no concurrency token, no domain events to exclude —
/// the architecture rule that forces the base onto aggregate roots deliberately leaves it alone.
/// </remarks>
public sealed class AccessPointConfiguration : IEntityTypeConfiguration<AccessPoint>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AccessPoint> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("AccessPoint");

        // The natural key is the fact itself: this form leads to that record. Two authors sharing a
        // form — homonyms are ordinary in a catalogue — are two rows, distinguished by TargetId.
        builder.HasKey(accessPoint => new { accessPoint.Kind, accessPoint.TargetId, accessPoint.Form });

        // Stored as its name rather than its number: a projection table is read by humans when
        // something looks wrong, and 'Author' answers where '0' asks.
        builder.Property(accessPoint => accessPoint.Kind)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(accessPoint => accessPoint.Form)
            .HasMaxLength(Math.Max(NameForm.MaxLength, Title.MaxLength))
            .IsRequired();

        // The one question this table exists for: which records answer to this form?
        builder.HasIndex(accessPoint => accessPoint.Form);
    }
}
