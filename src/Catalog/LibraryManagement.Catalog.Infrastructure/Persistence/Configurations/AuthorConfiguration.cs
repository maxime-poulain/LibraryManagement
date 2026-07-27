using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Author"/> to the store.
/// </summary>
/// <remarks>
/// The whole mapping lives here and nothing about it leaks into the domain: no attribute, no base
/// class, no navigation added for the mapper's benefit. An aggregate that had to be shaped for a
/// database would stop being a model of the business.
/// </remarks>
public sealed class AuthorConfiguration : AggregateRootConfiguration<Author, AuthorId>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<Author> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Authors");

        builder.Property(author => author.AuthorizedName)
            .HasConversion(name => name.Value, value => PersonNameOf(value))
            .HasMaxLength(PersonName.MaxLength)
            .IsRequired();

        // Filing depends on it, and a search for an author is the most common read in a catalogue.
        builder.HasIndex(author => author.AuthorizedName);

        builder.ComplexProperty(author => author.LifeYears, life =>
        {
            life.Property(years => years.Birth).HasColumnName("BirthYear");
            life.Property(years => years.Death).HasColumnName("DeathYear");
        });

        // A table of its own rather than a JSON column: a variant name is what a reader searches by
        // when they only know a name the author no longer uses, and that search wants an index.
        builder.OwnsMany(author => author.VariantNames, variant =>
        {
            variant.ToTable("AuthorVariantNames");
            variant.WithOwner().HasForeignKey("AuthorId");
            variant.Property(name => name.Value)
                .HasColumnName("Name")
                .HasMaxLength(PersonName.MaxLength)
                .IsRequired();
            variant.HasKey("AuthorId", "Value");
            variant.HasIndex(name => name.Value);
        });

        builder.Navigation(author => author.VariantNames)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    // A stored name has already been through PersonName.Create once. Re-validating on the way back
    // would turn a corrupt row into an exception no caller can act on, so materialization trusts
    // the store and any repair belongs in a migration.
    private static PersonName PersonNameOf(string value)
        => PersonName.Create(value).Match(name => name, _ => throw new InvalidOperationException(
            $"The catalogue holds '{value}' as a name, which is not a valid one."));
}
