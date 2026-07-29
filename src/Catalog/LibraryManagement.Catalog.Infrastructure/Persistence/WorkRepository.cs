using LibraryManagement.Catalog.Domain.Works;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Persistence;

/// <summary>
/// Implements <see cref="IWorkRepository"/> over <see cref="CatalogDbContext"/>.
/// </summary>
/// <param name="context">The module's store.</param>
public sealed class WorkRepository(CatalogDbContext context) : IWorkRepository
{
    /// <inheritdoc/>
    public async ValueTask<Work?> GetByIdAsync(WorkId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await context.Works
            .Include(work => work.AuthorIds)
            .FirstOrDefaultAsync(work => work.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ExistsAsync(WorkId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await context.Works
            .AnyAsync(work => work.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>Add</c> and not <c>DbSet.AddAsync</c>, for the reason given on
    /// <see cref="AuthorRepository.Add"/>.
    /// </remarks>
    public void Add(Work work)
    {
        ArgumentNullException.ThrowIfNull(work);

        context.Works.Add(work);
    }
}
