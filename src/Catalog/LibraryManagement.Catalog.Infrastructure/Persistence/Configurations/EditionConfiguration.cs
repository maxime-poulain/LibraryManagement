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

        // "The editions of this work" is the question a catalog asks of this table most.
        builder.HasIndex(edition => edition.WorkId);

        builder.Property(edition => edition.Isbn)
            .HasConversion(isbn => isbn!.Value, value => IsbnOf(value))
            .HasMaxLength(Isbn.MaxLength);

        // Indexed but deliberately not unique: publishers do reuse ISBNs, and forbidding a
        // duplicate is a cataloging decision the domain has not made — an index is not where it
        // would be made.
        builder.HasIndex(edition => edition.Isbn);

        // The survivor of a merge, and null while this record is one in its own right. An
        // identifier column and not a foreign key, exactly as WorkId above is — and here the
        // reason is sharper than consistency: the column points at another row of this same table,
        // so a constraint would be possible, and it is still refused. A merged-away record must
        // stay readable if the survivor is ever itself corrected, and a cascade is a mechanism for
        // deleting things this context does not delete.
        builder.Property(edition => edition.AbsorbedInto)
            .HasConversion(id => id!.Value, value => EditionId.Create(value));
    }

    private static Isbn IsbnOf(string value)
        => Isbn.Create(value).Match(isbn => isbn, _ => throw new InvalidOperationException(
            $"The catalog holds '{value}' as an ISBN, which is not a valid one."));
}
