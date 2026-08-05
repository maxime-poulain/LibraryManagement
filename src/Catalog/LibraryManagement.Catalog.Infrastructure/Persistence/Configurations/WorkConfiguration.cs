using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Work"/> to the store.
/// </summary>
public sealed class WorkConfiguration : AggregateRootConfiguration<Work, WorkId>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<Work> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Work");

        builder.Property(work => work.PreferredTitle)
            .HasConversion(title => title.Value, value => TitleOf(value))
            .HasMaxLength(Title.MaxLength)
            .IsRequired();

        builder.HasIndex(work => work.PreferredTitle);

        // A join table rather than a JSON array of identifiers. "Everything by this author" is a
        // question the catalog is asked constantly, and an array cannot be indexed for it.
        //
        // There is no navigation from Work to Author, and there will not be. They are separate
        // aggregates: a work holds the identity of its authors and never the authors themselves,
        // so loading one can never drag the other along.
        builder.OwnsMany(work => work.AuthorIds, credit =>
        {
            credit.ToTable("WorkAuthor");
            credit.WithOwner().HasForeignKey("WorkId");
            // The identifier is the author's own and the engine never invents one. Said explicitly
            // because the convention reads a Guid key as store-generated, and a credit added to a
            // work already on file then arrives with its key non-default: EF concludes the row
            // exists, marks it Modified, and — the table being nothing but its key — has nothing
            // to update and writes no statement at all. The credit would vanish without an error.
            credit.Property(id => id.Value).HasColumnName("AuthorId").ValueGeneratedNever();
            credit.HasKey("WorkId", "Value");
            credit.HasIndex(id => id.Value);
        });

        builder.Navigation(work => work.AuthorIds)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private static Title TitleOf(string value)
        => Title.Create(value).Match(title => title, _ => throw new InvalidOperationException(
            $"The catalog holds '{value}' as a title, which is not a valid one."));
}
