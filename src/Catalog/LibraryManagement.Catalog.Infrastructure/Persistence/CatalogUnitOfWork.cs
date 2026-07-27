using LibraryManagement.Shared.Application;

namespace LibraryManagement.Catalog.Infrastructure.Persistence;

/// <summary>
/// Writes what a Catalog command changed, through the module's own store.
/// </summary>
/// <param name="context">The module's context.</param>
/// <remarks>
/// <para>
/// Registered against the assembly declaring the module's commands, so the shared pipeline finds
/// this one for a Catalog command and another module's for theirs.
/// </para>
/// <para>
/// <c>SaveChangesAsync</c> is called here and nowhere else. No handler calls it, no repository calls
/// it, and none of them can forget to: the pipeline writes only when the command reports success,
/// so "the command succeeded" and "the work was written" become one event. A handler that saved for
/// itself would break that — half its work could survive the other half failing, and the failure it
/// returned would be contradicted by what the database holds.
/// </para>
/// </remarks>
public sealed class CatalogUnitOfWork(CatalogDbContext context) : IUnitOfWork
{
    /// <inheritdoc/>
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
