using LibraryManagement.Shared.Application;

namespace LibraryManagement.Circulation.Infrastructure.Persistence;

/// <summary>
/// Writes what a Circulation command changed, through the module's own store.
/// </summary>
/// <param name="context">The module's context.</param>
/// <remarks>
/// Registered against the assembly declaring this module's commands. The one save is what makes
/// the return moment whole: the loan closes and the queue traps in a single transaction, which is
/// the entire reason holds and loans live in one context.
/// </remarks>
public sealed class CirculationUnitOfWork(CirculationDbContext context) : IUnitOfWork
{
    /// <inheritdoc/>
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
