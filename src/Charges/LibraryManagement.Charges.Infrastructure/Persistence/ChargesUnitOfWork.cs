using LibraryManagement.Shared.Application;

namespace LibraryManagement.Charges.Infrastructure.Persistence;

/// <summary>
/// Writes what a Charges command changed, through the module's own store.
/// </summary>
/// <param name="context">The module's context.</param>
/// <remarks>
/// Registered against the assembly declaring this module's commands. It matters more here than
/// anywhere: a Charges command is most often dispatched from a subscriber running inside
/// <em>Circulation's</em> drain, and the keyed resolver is what sends the save to this store rather
/// than to the one the drain happens to be holding.
/// </remarks>
public sealed class ChargesUnitOfWork(ChargesDbContext context) : IUnitOfWork
{
    /// <inheritdoc/>
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
