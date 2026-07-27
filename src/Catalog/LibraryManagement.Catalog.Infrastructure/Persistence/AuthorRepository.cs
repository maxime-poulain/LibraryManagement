using LibraryManagement.Catalog.Domain.Authors;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Persistence;

/// <summary>
/// Implements <see cref="IAuthorRepository"/> over <see cref="CatalogDbContext"/>.
/// </summary>
/// <param name="context">The module's store.</param>
public sealed class AuthorRepository(CatalogDbContext context) : IAuthorRepository
{
    /// <inheritdoc/>
    public async ValueTask<Author?> GetByIdAsync(
        AuthorId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        // The variant names come with the record. They are part of the aggregate, not a detail to
        // fetch later: a rename reads and rewrites them, so loading half an author would let a
        // change be made against a state that was never true.
        return await context.Authors
            .Include(author => author.VariantNames)
            .FirstOrDefaultAsync(author => author.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ExistsAsync(
        AuthorId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await context.Authors
            .AnyAsync(author => author.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>Add</c> and not <c>DbSet.AddAsync</c>: that overload exists for value generators which
    /// must reach the database to produce a key, such as a HiLo sequence, and it would open a round
    /// trip with nothing to fetch.
    /// </remarks>
    public void Add(Author author)
    {
        ArgumentNullException.ThrowIfNull(author);

        // Tracked, not written. The module's unit of work writes once the command has succeeded.
        context.Authors.Add(author);
    }
}
