using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Catalog.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Edition"/> to the store.
/// </summary>
public sealed class EditionConfiguration : AggregateRootConfiguration<Edition, EditionId>
{
    /// <inheritdoc/>
    protected override void ConfigureAggregate(EntityTypeBuilder<Edition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Edition");

        // An identifier column, not a foreign key — the same stance WorkAuthor takes toward
        // authors: aggregates reference each other by identity, and the rule that the work exists
        // is enforced where it can be explained, in the handler.
        builder.Property(edition => edition.WorkId)
            .HasConversion(id => id.Value, value => WorkId.Create(value))
            .IsRequired();

        // "The editions of this work" is the question a catalogue asks of this table most.
        builder.HasIndex(edition => edition.WorkId);

        builder.Property(edition => edition.Isbn)
            .HasConversion(isbn => isbn!.Value, value => IsbnOf(value))
            .HasMaxLength(Isbn.MaxLength);

        // Indexed but deliberately not unique: publishers do reuse ISBNs, and forbidding a
        // duplicate is a cataloguing decision the domain has not made — an index is not where it
        // would be made.
        builder.HasIndex(edition => edition.Isbn);
    }

    private static Isbn IsbnOf(string value)
        => Isbn.Create(value).Match(isbn => isbn, _ => throw new InvalidOperationException(
            $"The catalogue holds '{value}' as an ISBN, which is not a valid one."));
}
