using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.DomainEvents;

namespace LibraryManagement.Catalog.Infrastructure.Search;

// The edition slice of the access-point index. The rules are the ones AuthorAccessPointHandlers
// states: never save, converge rather than accumulate, carry no invariant.

/// <summary>
/// An edition cataloged: its ISBN, when it bears one, becomes findable.
/// </summary>
/// <remarks>
/// An edition without an ISBN adds nothing, and that is not an error: it has no form to answer to
/// yet. Nothing else about the edition is indexed here — the work's title already is, through the
/// work's own access point, and duplicating it per edition would make every retitle fan out over
/// rows that add no way in.
/// </remarks>
public sealed class EditionRegisteredProjector(CatalogDbContext context)
    : IDomainEventHandler<EditionRegistered>
{
    /// <inheritdoc/>
    public async ValueTask Handle(EditionRegistered notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.Isbn is null)
        {
            return;
        }

        await context.EnsureAccessPointAsync(
            AccessPointKind.Edition,
            notification.EditionId.Value,
            notification.Isbn.Value,
            preferred: true,
            cancellationToken).ConfigureAwait(false);
    }
}
