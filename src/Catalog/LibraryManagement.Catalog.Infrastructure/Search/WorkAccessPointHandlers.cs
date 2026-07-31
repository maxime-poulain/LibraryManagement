using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.DomainEvents;

namespace LibraryManagement.Catalog.Infrastructure.Search;

// The work half of the access-point index. The rules are the ones AuthorAccessPointHandlers states:
// never save, converge rather than accumulate, carry no invariant.

/// <summary>
/// A work catalogued: its title becomes findable.
/// </summary>
public sealed class WorkRegisteredProjector(CatalogDbContext context)
    : IDomainEventHandler<WorkRegistered>
{
    /// <inheritdoc/>
    public async ValueTask Handle(WorkRegistered notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await context.EnsureAccessPointAsync(
            AccessPointKind.Work,
            notification.WorkId.Value,
            notification.Title.Value,
            preferred: true,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// A work retitled: the new title replaces the old one outright.
/// </summary>
/// <remarks>
/// A replacement and not a demotion, mirroring the model exactly: a retitled work keeps no memory
/// of its former title, so neither does the index. The day former titles must stay findable, the
/// aggregate gains title variants first and this projector follows.
/// </remarks>
public sealed class WorkRetitledProjector(CatalogDbContext context)
    : IDomainEventHandler<WorkRetitled>
{
    /// <inheritdoc/>
    public async ValueTask Handle(WorkRetitled notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await context.RemoveAccessPointAsync(
            AccessPointKind.Work,
            notification.WorkId.Value,
            notification.PreviousTitle.Value,
            cancellationToken).ConfigureAwait(false);

        await context.EnsureAccessPointAsync(
            AccessPointKind.Work,
            notification.WorkId.Value,
            notification.NewTitle.Value,
            preferred: true,
            cancellationToken).ConfigureAwait(false);
    }
}
