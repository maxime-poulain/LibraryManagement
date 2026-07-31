namespace LibraryManagement.Catalog.PublishedLanguage;

/// <summary>
/// What Catalog says about its editions to the contexts downstream of it.
/// </summary>
/// <remarks>
/// <para>
/// The published language of the Catalog → Holdings edge, in the strategic design's terms: Holdings
/// needs to <em>name</em> an edition, never to describe one, and an identifier is the whole of what
/// crosses today. The summary this project is also meant to carry — a title, a statement of
/// responsibility, enough to render a line without reproducing a record — arrives when something
/// asks for it.
/// </para>
/// <para>
/// It is deliberately not an anticorruption layer. One protects a model where a foreign context's
/// concepts feed decisions, and nothing of Catalog's reaches a decision here: an identifier goes
/// out and a yes or no comes back. The day the summary crosses, a Catalog concept starts feeding
/// what Holdings displays and composes, and an anticorruption layer becomes right — on Holdings'
/// side, where the downstream owns its translation.
/// </para>
/// </remarks>
public interface IEditionCatalog
{
    /// <summary>
    /// Determines whether an edition is cataloged under an identifier.
    /// </summary>
    /// <param name="editionId">The edition to look for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> when the edition exists; <see langword="false"/> otherwise.
    /// </returns>
    /// <remarks>
    /// A <see cref="Guid"/> and not Catalog's own <c>EditionId</c>. That type belongs to the
    /// module's domain, and a published surface handing it out would make every consumer compile
    /// against the model this project exists to keep in. It is the same rule the query contracts
    /// already follow, where a <c>Dto</c> carries primitives for the same reason.
    /// </remarks>
    ValueTask<bool> ExistsAsync(Guid editionId, CancellationToken cancellationToken = default);
}
