namespace LibraryManagement.Catalog.Infrastructure.Search;

/// <summary>
/// What a record can be found by. The catalog's own word: every preferred name, variant form and title
/// is an access point, and a search resolves a form to the record it leads back to.
/// </summary>
/// <remarks>
/// <para>
/// A read-model row, deliberately anemic like <c>OutboxMessage</c>: no invariants, no behavior.
/// The truth lives in the aggregates; this table is a projection of it, fed by their events through
/// the outbox, and rebuildable from them. It answers the question the write model is shaped wrong
/// for — "which records answer to this form?" — without loading a single aggregate.
/// </para>
/// <para>
/// This is the Catalog-local slice of the read model the strategic design describes: Search is not
/// a bounded context but a projection, and the cross-module list — titles, copies, availability —
/// will be composed the day the other modules exist. Nothing here crosses a boundary; Catalog's
/// events feed Catalog's table, in Catalog's schema.
/// </para>
/// </remarks>
public sealed class AccessPoint
{
    /// <summary>Whether the form leads to an author or to a work.</summary>
    public AccessPointKind Kind { get; set; }

    /// <summary>The record the form leads back to.</summary>
    public Guid TargetId { get; set; }

    /// <summary>The form itself — a preferred name, a variant, a title.</summary>
    public string Form { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is the form the catalog files the record under, rather than a variant that
    /// leads to it. A search matches both; a result list displays the preferred one.
    /// </summary>
    public bool IsPreferred { get; set; }
}

/// <summary>
/// The kinds of record an <see cref="AccessPoint"/> can lead to.
/// </summary>
public enum AccessPointKind
{
    /// <summary>The form leads to an author record.</summary>
    Author,

    /// <summary>The form leads to a work.</summary>
    Work,

    /// <summary>The form leads to an edition — an ISBN.</summary>
    Edition,
}
