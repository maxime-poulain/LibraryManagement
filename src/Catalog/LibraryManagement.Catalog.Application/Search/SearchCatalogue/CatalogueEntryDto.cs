namespace LibraryManagement.Catalog.Application.Search.SearchCatalogue;

/// <summary>
/// One line of the catalogue's index: a form, and the record it leads back to.
/// </summary>
/// <param name="Kind">Whether the form leads to an author or to a work.</param>
/// <param name="RecordId">The record the form leads back to.</param>
/// <param name="Form">The form that matched the search — a heading, a variant, a title.</param>
/// <param name="AuthorizedForm">The form the catalogue files the record under.</param>
/// <remarks>
/// <para>
/// The unit is the form, not the record, exactly as in the profession's own index: every access
/// point is a line of its own. Two homonymous authors are two lines a reader tells apart by the
/// record; one author matched through two of their forms is two lines, each a legitimate way in.
/// </para>
/// <para>
/// When <paramref name="Form"/> and <paramref name="AuthorizedForm"/> differ, the line is a
/// see-reference — <c>Sullivan, Vernon</c> <em>see</em> <c>Vian, Boris</c> — which is the entire
/// service a variant exists to render. When they are equal, the line is the entry itself.
/// </para>
/// </remarks>
public sealed record CatalogueEntryDto(
    CatalogueEntryKind Kind,
    Guid RecordId,
    string Form,
    string AuthorizedForm);

/// <summary>
/// The kinds of record a catalogue search can lead to.
/// </summary>
/// <remarks>
/// Owned by the contract, not borrowed from the storage row's own enum: the application cannot
/// reference the infrastructure, and a caller of this query must not have to either.
/// </remarks>
public enum CatalogueEntryKind
{
    /// <summary>The line leads to an author record.</summary>
    Author,

    /// <summary>The line leads to a work.</summary>
    Work,
}
