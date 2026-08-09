using LibraryManagement.Charges.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Charges.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the two kinds of charge to one table, told apart by a column that says which.
/// </summary>
/// <remarks>
/// <para>
/// <strong>An entity of its own rather than an owned collection</strong>, unlike every other child
/// in this solution. Entity Framework does not support inheritance on owned types, and the two kinds
/// of charge are two types by design — so the aggregate boundary here is held by the code, which
/// reaches a charge only through its account, rather than by the mapping.
/// </para>
/// <para>
/// <strong>Table-per-hierarchy, with the kind stored as its name.</strong> One table, because the
/// two share everything the store cares about; the name rather than a number, because a human reads
/// this table when something looks wrong and <c>ReplacementCharge</c> answers where <c>1</c> asks.
/// </para>
/// <para>
/// <strong>The key says <c>ValueGeneratedNever</c>.</strong> Identifiers come from the domain, and
/// the convention would read a <c>Guid</c> key as store-generated — which makes a charge added to an
/// account the store already holds arrive marked <c>Modified</c> rather than <c>Added</c>. That
/// defect cost a release to find once already.
/// </para>
/// </remarks>
public sealed class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    /// <summary>The largest amount a column here will hold, and its precision.</summary>
    private const int MoneyPrecision = 18;

    private const int MoneyScale = 2;

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Charge> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Charge");

        builder.HasKey(charge => charge.Id);

        builder.Property(charge => charge.Id)
            .HasConversion(id => id.Value, value => ChargeId.Create(value))
            .ValueGeneratedNever();

        builder.HasDiscriminator<string>("Kind")
            .HasValue<OverdueFine>(nameof(OverdueFine))
            .HasValue<ReplacementCharge>(nameof(ReplacementCharge))
            .HasValue<DamageCharge>(nameof(DamageCharge));

        builder.Property("Kind").HasMaxLength(32);

        builder.Property(charge => charge.Amount)
            .HasConversion(amount => amount.Amount, value => Money.Of(value))
            .HasPrecision(MoneyPrecision, MoneyScale)
            .IsRequired();

        builder.Property(charge => charge.Paid)
            .HasConversion(paid => paid.Amount, value => Money.Of(value))
            .HasPrecision(MoneyPrecision, MoneyScale)
            .IsRequired();

        // Identifier columns, not foreign keys: they point into two other schemas, and no
        // constraint crosses a schema here.
        builder.Property(charge => charge.LoanId)
            .HasConversion(id => id.Value, value => LoanId.Create(value))
            .IsRequired();

        builder.Property(charge => charge.CopyId)
            .HasConversion(id => id.Value, value => CopyId.Create(value))
            .IsRequired();

        // How a found copy reaches the charge it undoes. Filtered to the kind that can be undone,
        // because a fine has nothing to do with a copy turning up.
        builder.HasIndex(charge => charge.CopyId)
            .HasFilter("[Kind] = N'ReplacementCharge'");

        builder.Property(charge => charge.IncurredOn).IsRequired();

        // Both computed from the two amounts above; a column for either would be a second truth.
        builder.Ignore(charge => charge.Outstanding);
        builder.Ignore(charge => charge.IsSettled);

        // OverdueFine.DaysLate needs nothing said about it: in a hierarchy mapped to one table, a
        // property of one subtype becomes a nullable column that the other subtype leaves alone.
    }
}
