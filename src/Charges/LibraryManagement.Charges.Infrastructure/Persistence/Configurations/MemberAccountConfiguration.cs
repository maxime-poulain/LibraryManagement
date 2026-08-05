using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Charges.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="MemberAccount"/> to the store.
/// </summary>
/// <remarks>
/// The account row carries nothing but its identity and the audit trail: the account <em>is</em> its
/// outstanding charges, and the balance is computed from them rather than stored. A total column
/// here would be a second source of one truth, and the day it disagreed nothing would say which was
/// right.
/// </remarks>
public sealed class MemberAccountConfiguration : AggregateRootConfiguration<MemberAccount, MemberId>
{
    /// <summary>The column the charge table points back through.</summary>
    internal const string AccountForeignKey = "MemberId";

    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<MemberAccount> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MemberAccount");

        // Computed from the charges, never a column.
        builder.Ignore(account => account.Balance);

        builder.HasMany(account => account.Charges)
            .WithOne()
            .HasForeignKey(AccountForeignKey)
            .OnDelete(DeleteBehavior.Cascade);

        // The aggregate exposes a read-only view over its own list, so the store must write through
        // the field rather than the property it could never add to.
        builder.Navigation(account => account.Charges)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
