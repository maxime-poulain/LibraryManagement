using LibraryManagement.Catalog.Infrastructure.Persistence;

namespace LibraryManagement.Catalog.Infrastructure.Search;

/// <summary>
/// The two moves every projector is made of: make sure a form leads to a record, and make sure it
/// no longer does.
/// </summary>
/// <remarks>
/// Both converge instead of accumulating, which is what makes every handler idempotent for free:
/// ensuring an access point that exists updates its standing, removing one that is gone does
/// nothing, and a redelivered event lands on the state it already produced. <c>FindAsync</c> is
/// used deliberately — it sees rows added earlier in the same drain save, so two events of one
/// chain touching the same form compose instead of colliding.
/// </remarks>
internal static class AccessPointOperations
{
    internal static async ValueTask EnsureAccessPointAsync(
        this CatalogDbContext context,
        AccessPointKind kind,
        Guid targetId,
        string form,
        bool preferred,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<AccessPoint>()
            .FindAsync([kind, targetId, form], cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            context.Set<AccessPoint>().Add(new AccessPoint
            {
                Kind = kind,
                TargetId = targetId,
                Form = form,
                IsPreferred = preferred,
            });

            return;
        }

        existing.IsPreferred = preferred;
    }

    internal static async ValueTask RemoveAccessPointAsync(
        this CatalogDbContext context,
        AccessPointKind kind,
        Guid targetId,
        string form,
        CancellationToken cancellationToken)
    {
        var existing = await context.Set<AccessPoint>()
            .FindAsync([kind, targetId, form], cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            context.Set<AccessPoint>().Remove(existing);
        }
    }
}
