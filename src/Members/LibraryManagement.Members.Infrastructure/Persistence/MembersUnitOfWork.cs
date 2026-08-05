using LibraryManagement.Shared.Application;

namespace LibraryManagement.Members.Infrastructure.Persistence;

/// <summary>
/// Writes what a Members command changed, through the module's own store.
/// </summary>
/// <param name="context">The module's context.</param>
/// <remarks>
/// Registered against the assembly declaring this module's commands, the third registration of
/// one interface in the solution — which is exactly the arrangement the keyed resolver exists
/// for, and why none of the three can answer for another.
/// </remarks>
public sealed class MembersUnitOfWork(MembersDbContext context) : IUnitOfWork
{
    /// <inheritdoc/>
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}
