using LibraryManagement.Catalog.Application.Search.SearchCatalogue;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Infrastructure.Search;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Queries;

/// <summary>
/// Handles <see cref="SearchCatalogueQuery"/> against the access-point index.
/// </summary>
/// <param name="context">The module's store.</param>
/// <remarks>
/// <para>
/// In the infrastructure for the reason given on <see cref="GetWorkByIdQueryHandler"/>, and reading
/// the table the outbox feeds rather than the aggregates: the index is eventually consistent by
/// design, so a record registered a moment ago answers only once the drain has run — the few
/// seconds the strategic design's own boundary test says nobody at a desk notices.
/// </para>
/// <para>
/// An empty answer is a success, where <see cref="GetWorkByIdQueryHandler"/> fails. That handler is
/// asked about one record by identity, and absence means the caller's reference is wrong; this one
/// is asked a criterion, and "nothing answers to that" answers it.
/// </para>
/// </remarks>
public sealed class SearchCatalogueQueryHandler(CatalogDbContext context)
    : IQueryHandler<SearchCatalogueQuery, IReadOnlyList<CatalogueEntryDto>>
{
    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<CatalogueEntryDto>>> Handle(
        SearchCatalogueQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A blank term never arrives here: the validator rejects it at dispatch. Reaching this
        // handler with one is a wiring mistake, and it is reported as one — a prefix of nothing
        // would otherwise match the entire index, which no caller has asked for.
        ArgumentException.ThrowIfNullOrWhiteSpace(query.SearchTerm);

        // Trimmed because every stored form is: PersonName and Title trim on creation, so the
        // space a search box leaves behind would otherwise miss what the catalogue holds.
        var term = query.SearchTerm.Trim();

        // StartsWith and not a raw LIKE: Entity Framework escapes the pattern characters, so a
        // term containing '%' or '_' means those characters. The term is typed by an employee,
        // and an unescaped wildcard would quietly turn '100%' into everything.
        //
        // The self-join turns a matched variant into a see-reference. Every record carries
        // exactly one authorized form — the projectors maintain that — so the join is one to
        // one: the line shows the form that matched and the heading it leads to.
        var lines = await context.Set<AccessPoint>()
            .AsNoTracking()
            .Where(point => point.Form.StartsWith(term))
            .Join(
                context.Set<AccessPoint>().Where(heading => heading.IsAuthorized),
                point => new { point.Kind, point.TargetId },
                heading => new { heading.Kind, heading.TargetId },
                (point, heading) => new
                {
                    point.Kind,
                    point.TargetId,
                    point.Form,
                    AuthorizedForm = heading.Form,
                })
            .OrderBy(line => line.Form)
            .ThenBy(line => line.Kind)
            .ThenBy(line => line.TargetId)
            .Take(SearchCatalogueQuery.MaxResults)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Authors and works interfile in one alphabet — the dictionary catalogue, which is what a
        // desk actually wants: whoever typed 'Mille' should not have to say whether they are
        // looking for a title or a person before they may look.
        IReadOnlyList<CatalogueEntryDto> entries = lines
            .Select(line => new CatalogueEntryDto(KindOf(line.Kind), line.TargetId, line.Form, line.AuthorizedForm))
            .ToArray();

        return Result<IReadOnlyList<CatalogueEntryDto>>.Success(entries);
    }

    // The contract's kind, not the row's: the storage enum cannot leave the module. An unknown
    // kind is corruption, reported as such — the same trust-the-store stance the serializer takes.
    private static CatalogueEntryKind KindOf(AccessPointKind kind)
        => kind switch
        {
            AccessPointKind.Author => CatalogueEntryKind.Author,
            AccessPointKind.Work => CatalogueEntryKind.Work,
            AccessPointKind.Edition => CatalogueEntryKind.Edition,
            _ => throw new InvalidOperationException($"Unknown access point kind '{kind}'."),
        };
}
