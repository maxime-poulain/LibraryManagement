using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.DomainEvents;
using Microsoft.EntityFrameworkCore;

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

/// <summary>
/// Two records became one: the absorbed record's forms stop leading to it and start leading to the
/// survivor, as variants.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The forms move rather than disappear.</strong> An ISBN somebody used to find the
/// absorbed record is still a true way in — it is printed on the book in their hand — and a merge
/// that deleted it would make a correction of the catalog look, to the person searching, exactly
/// like the record having been lost. So each form is re-pointed at the survivor, which is what a
/// catalog does with the identifiers of a record it absorbs.
/// </para>
/// <para>
/// <strong>They arrive as variants and never as preferred.</strong> The survivor already has its
/// own ISBN, marked preferred when it was cataloged; a second preferred form would make two answers
/// to *which form does this record answer to?* and the index has no way to choose. The distinction
/// is the one the glossary already draws between a preferred name and a variant, applied to the
/// thing a merge produces.
/// </para>
/// <para>
/// Reached from the absorbed identifier rather than from the edition, so this needs no lookup of an
/// aggregate it must not touch: whatever forms led there are exactly the rows to move, whether or
/// not the record still knows why it has them.
/// </para>
/// </remarks>
public sealed class EditionsMergedProjector(CatalogDbContext context)
    : IDomainEventHandler<EditionsMerged>
{
    /// <inheritdoc/>
    public async ValueTask Handle(EditionsMerged notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var absorbed = notification.AbsorbedEditionId.Value;

        var forms = await context.Set<AccessPoint>()
            .Where(point => point.Kind == AccessPointKind.Edition && point.TargetId == absorbed)
            .Select(point => point.Form)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var form in forms)
        {
            await context.RemoveAccessPointAsync(
                AccessPointKind.Edition,
                absorbed,
                form,
                cancellationToken).ConfigureAwait(false);

            await context.EnsureAccessPointAsync(
                AccessPointKind.Edition,
                notification.SurvivingEditionId.Value,
                form,
                preferred: false,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
