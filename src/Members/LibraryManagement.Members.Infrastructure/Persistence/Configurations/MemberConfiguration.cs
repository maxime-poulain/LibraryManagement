using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Members.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Member"/> to the store.
/// </summary>
/// <remarks>
/// The whole mapping lives here and nothing about it leaks into the domain: no attribute, no base
/// class, no navigation added for the mapper's benefit. Everything a member holds is a value on
/// the row itself — the name, the channels and the guardian spread into columns, because this
/// table is read by a human when something looks wrong, and a column answers where a blob asks.
/// </remarks>
public sealed class MemberConfiguration : AggregateRootConfiguration<Member, MemberId>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<Member> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Member");

        builder.ComplexProperty(member => member.Name, name =>
        {
            // Optional as a whole since erasure arrived — the same presence bit the guardian
            // carries, and for the same reason: the name's parts are values, and only a direct
            // column can tell "no name any more" from a name it never heard about.
            name.HasDiscriminator<bool>("Present").HasValue(true);

            name.Property(part => part.GivenName)
                .HasColumnName("GivenName")
                .HasMaxLength(MemberName.MaxLength)
                .IsRequired();

            name.Property(part => part.FamilyName)
                .HasColumnName("FamilyName")
                .HasMaxLength(MemberName.MaxLength)
                .IsRequired();
        });

        // Required at enrollment by the aggregate, nullable in the store because an erasure
        // takes it: the column must be able to say the fact is gone.
        builder.Property(member => member.DateOfBirth);

        // Stored as its name rather than its number: a category is read by a human when something
        // looks wrong, and 'Child' answers where '1' asks.
        builder.Property(member => member.Category)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(member => member.CardNumber)
            .HasConversion(cardNumber => cardNumber!.Value, value => CardNumberOf(value))
            .HasMaxLength(CardNumber.MaxLength);

        // Unique, and this index is what actually holds the rule. The handler asks first so the
        // refusal can name the number; a constraint violation cannot say which member already has
        // it, and this is what makes sure the answer stays true between the asking and the
        // writing. Filtered, because erasure retires a card to null and erased members are
        // legion-in-waiting: an unfiltered unique index would allow exactly one of them.
        builder.HasIndex(member => member.CardNumber)
            .IsUnique()
            .HasFilter("[CardNumber] IS NOT NULL");

        builder.Property(member => member.MembershipStart).IsRequired();
        builder.Property(member => member.MembershipEnd).IsRequired();
        builder.Property(member => member.ErasedOn);

        builder.ComplexProperty(member => member.ContactDetails, contact =>
        {
            contact.Property(channels => channels.Email)
                .HasColumnName("Email")
                .HasMaxLength(ContactDetails.MaxEmailLength);

            contact.Property(channels => channels.Phone)
                .HasColumnName("Phone")
                .HasMaxLength(ContactDetails.MaxPhoneLength);

            contact.Property(channels => channels.PostalAddress)
                .HasColumnName("PostalAddress")
                .HasMaxLength(ContactDetails.MaxPostalAddressLength);
        });

        // Optional as a whole — most members are reached directly — and its columns are prefixed
        // so a guardian's email can never be misread as the member's own.
        builder.ComplexProperty(member => member.Guardian, guardian =>
        {
            // The presence column, Guardian_Present once the engine prefixes it. The store cannot
            // tell "no guardian" from "a guardian whose every column is null" on its own: the
            // guardian's direct members are themselves values, and only direct properties can
            // discriminate. A shadow discriminator is the engine's way of writing the fact down —
            // configured as a bit named for the question it answers, because the default is an
            // unbounded string named for the machinery.
            guardian.HasDiscriminator<bool>("Present").HasValue(true);

            guardian.ComplexProperty(g => g.Name, name =>
            {
                // Required within an optional whole: a guardian always has a name, and these two
                // columns are what lets the store tell "no guardian" from one — null name, no
                // guardian. The engine still makes the columns nullable, since the whole may be
                // absent.
                name.Property(part => part.GivenName)
                    .HasColumnName("GuardianGivenName")
                    .HasMaxLength(MemberName.MaxLength)
                    .IsRequired();

                name.Property(part => part.FamilyName)
                    .HasColumnName("GuardianFamilyName")
                    .HasMaxLength(MemberName.MaxLength)
                    .IsRequired();
            });

            guardian.ComplexProperty(g => g.Contact, contact =>
            {
                contact.Property(channels => channels.Email)
                    .HasColumnName("GuardianEmail")
                    .HasMaxLength(ContactDetails.MaxEmailLength);

                contact.Property(channels => channels.Phone)
                    .HasColumnName("GuardianPhone")
                    .HasMaxLength(ContactDetails.MaxPhoneLength);

                contact.Property(channels => channels.PostalAddress)
                    .HasColumnName("GuardianPostalAddress")
                    .HasMaxLength(ContactDetails.MaxPostalAddressLength);
            });
        });
    }

    // A stored value has already been through Create once. Re-validating on the way back would
    // turn a corrupt row into an exception no caller can act on, so materialization trusts the
    // store and any repair belongs in a migration.
    private static CardNumber CardNumberOf(string value)
        => CardNumber.Create(value).Match(cardNumber => cardNumber, _ => throw new InvalidOperationException(
            $"The registry holds '{value}' as a card number, which is not a valid one."));
}
