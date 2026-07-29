using LibraryManagement.Catalog.Domain.Editions;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Persistence;

/// <summary>
/// Implements <see cref="IEditionRepository"/> over <see cref="CatalogDbContext"/>.
/// </summary>
/// <param name="context">The module's store.</param>
public sealed class EditionRepository(CatalogDbContext context) : IEditionRepository
{
    /// <inheritdoc/>
    public async ValueTask<Edition?> GetByIdAsync(EditionId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await context.Editions
            .FirstOrDefaultAsync(edition => edition.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>Add</c> and not <c>DbSet.AddAsync</c>, for the reason given on
    /// <see cref="AuthorRepository.Add"/>.
    /// </remarks>
    public void Add(Edition edition)
    {
        ArgumentNullException.ThrowIfNull(edition);

        context.Editions.Add(edition);
    }
}
