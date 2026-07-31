using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.DomainEvents;

namespace LibraryManagement.Catalog.Infrastructure.Search;

// The handlers that keep the author half of the access-point index true. They live in the
// infrastructure for the reason the query handlers do: a projection has no domain model to protect,
// and a port between a handler and the table it maintains would isolate nothing. The events are the
// contract, and they are the domain's.
//
// Three rules bind every handler here, all consequences of the outbox that delivers to them:
//
//   - Never save. The drain's save carries a handler's writes together with the ProcessedOn mark of
//     the message that caused them — one save, so the mark can never part company with the effects.
//   - Be idempotent. Delivery is at-least-once, so each handler converges rather than accumulates:
//     ensure-a-row, not add-a-row.
//   - Carry no invariant. The index is eventually consistent by design; the truth stays in the
//     aggregates, and this table can be rebuilt from their events.

/// <summary>
/// A new authority record: its preferred name becomes findable.
/// </summary>
public sealed class AuthorRegisteredProjector(CatalogDbContext context)
    : IDomainEventHandler<AuthorRegistered>
{
    /// <inheritdoc/>
    public async ValueTask Handle(AuthorRegistered notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await context.EnsureAccessPointAsync(
            AccessPointKind.Author,
            notification.AuthorId.Value,
            notification.PreferredName.Value,
            preferred: true,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// A person renamed: the incoming form becomes the preferred one, and the outgoing form stays
/// findable as a variant — a book printed under it still bears it on its title page.
/// </summary>
public sealed class AuthorRenamedProjector(CatalogDbContext context)
    : IDomainEventHandler<AuthorRenamed>
{
    /// <inheritdoc/>
    public async ValueTask Handle(AuthorRenamed notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await context.EnsureAccessPointAsync(
            AccessPointKind.Author,
            notification.AuthorId.Value,
            notification.NewName.Value,
            preferred: true,
            cancellationToken).ConfigureAwait(false);

        await context.EnsureAccessPointAsync(
            AccessPointKind.Author,
            notification.AuthorId.Value,
            notification.PreviousName.Value,
            preferred: false,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// A preferred name corrected: the wrong form is retracted — not demoted — and the corrected one
/// takes its place.
/// </summary>
/// <remarks>
/// The whole reason <see cref="AuthorPreferredNameCorrected"/> is a different event from
/// <see cref="AuthorRenamed"/>: a rename keeps the outgoing form findable, a correction removes it.
/// A typo kept as an access point would preserve forever the one thing the catalog was asked to
/// remove.
/// </remarks>
public sealed class AuthorPreferredNameCorrectedProjector(CatalogDbContext context)
    : IDomainEventHandler<AuthorPreferredNameCorrected>
{
    /// <inheritdoc/>
    public async ValueTask Handle(AuthorPreferredNameCorrected notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await context.RemoveAccessPointAsync(
            AccessPointKind.Author,
            notification.AuthorId.Value,
            notification.PreviousName.Value,
            cancellationToken).ConfigureAwait(false);

        await context.EnsureAccessPointAsync(
            AccessPointKind.Author,
            notification.AuthorId.Value,
            notification.CorrectedName.Value,
            preferred: true,
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// A variant recorded: one more form leads back to the record — which is the entire reason
/// authority files record variants at all.
/// </summary>
public sealed class AuthorVariantNameAddedProjector(CatalogDbContext context)
    : IDomainEventHandler<AuthorVariantNameAdded>
{
    /// <inheritdoc/>
    public async ValueTask Handle(AuthorVariantNameAdded notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await context.EnsureAccessPointAsync(
            AccessPointKind.Author,
            notification.AuthorId.Value,
            notification.VariantName.Value,
            preferred: false,
            cancellationToken).ConfigureAwait(false);
    }
}
