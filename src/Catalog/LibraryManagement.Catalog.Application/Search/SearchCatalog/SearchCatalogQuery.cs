using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Catalog.Application.Search.SearchCatalog;

/// <summary>
/// Searches the catalog: which records answer to this form?
/// </summary>
/// <param name="FormPrefix">The beginning of the form being looked for.</param>
/// <remarks>
/// <para>
/// The search is a <em>prefix</em> match, because that is how a catalog is browsed: preferred names are
/// filed surname-first and titles as printed, precisely so that typing the beginning of either
/// walks the index. A substring search would answer more and rank nothing, and it could not use
/// the index that makes the table worth keeping.
/// </para>
/// <para>
/// The answer is the first <see cref="MaxResults"/> lines in filing order — the browse's first
/// page. Paging deliberately waits for the interface that needs it: a page size chosen without a
/// screen to show it on would be a guess wearing a number.
/// </para>
/// </remarks>
public sealed record SearchCatalogQuery(string FormPrefix) : IQuery<IReadOnlyList<CatalogEntryDto>>
{
    /// <summary>The most lines a single search answers with.</summary>
    public const int MaxResults = 50;
}
