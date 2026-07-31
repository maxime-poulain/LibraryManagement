using LibraryManagement.Shared.Application;

namespace LibraryManagement.Holdings.Infrastructure.Persistence;

/// <summary>
/// Writes what a Holdings command changed, through the module's own store.
/// </summary>
/// <param name="context">The module's context.</param>
/// <remarks>
/// Registered against the assembly declaring this module's commands. With Catalog registered the
/// same way, the shared pipeline now has two to choose between — and choosing correctly is the whole
/// reason the resolver is keyed rather than resolved by type, which until now no running code had
/// ever exercised.
/// </remarks>
public sealed class HoldingsUnitOfWork(HoldingsDbContext context) : IUnitOfWork
{
    /// <inheritdoc/>
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
