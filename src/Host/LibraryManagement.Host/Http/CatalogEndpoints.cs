using LibraryManagement.Catalog.Application.Authors.AddAuthorVariantName;
using LibraryManagement.Catalog.Application.Authors.CorrectAuthorLifeYears;
using LibraryManagement.Catalog.Application.Authors.CorrectAuthorPreferredName;
using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Application.Authors.RenameAuthor;
using LibraryManagement.Catalog.Application.Editions.MergeEditions;
using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Application.Search.SearchCatalog;
using LibraryManagement.Catalog.Application.Works.CreditAuthor;
using LibraryManagement.Catalog.Application.Works.GetWorkById;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Application.Works.RemoveAuthorCredit;
using LibraryManagement.Catalog.Application.Works.RetitleWork;
using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Host.Http;

/// <summary>
/// The catalog: what the library describes, and the only module that answers questions today.
/// </summary>
/// <remarks>
/// Creation is a POST on the plural resource; everything else is a POST on a named act. That reads
/// oddly to a strict REST ear and is the honest shape here: `Rename` and `CorrectPreferredName`
/// write the same field and differ only in what they leave behind, and a PATCH carrying a name
/// could not say which of the two happened. The route says it instead.
/// </remarks>
internal static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var catalog = routes.MapGroup("/catalog").WithTags("Catalog");

        catalog
            .Command<RegisterAuthorCommand>("/authors")
            .Command<RenameAuthorCommand>("/authors/rename")
            .Command<CorrectAuthorPreferredNameCommand>("/authors/correct-preferred-name")
            .Command<AddAuthorVariantNameCommand>("/authors/variant-names")
            .Command<CorrectAuthorLifeYearsCommand>("/authors/correct-life-years");

        catalog
            .Command<RegisterWorkCommand>("/works")
            .Command<RetitleWorkCommand>("/works/retitle")
            .Command<CreditAuthorCommand>("/works/credit-author")
            .Command<RemoveAuthorCreditCommand>("/works/remove-author-credit");

        catalog
            .Command<RegisterEditionCommand>("/editions")
            .Command<MergeEditionsCommand>("/editions/merge");

        catalog.MapGet(
            "/works/{workId:guid}",
            async (Guid workId, IQueryDispatcher queries, CancellationToken cancellationToken)
                => (await queries.DispatchAsync(new GetWorkByIdQuery(workId), cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult());

        catalog.MapGet(
            "/search",
            async (string formPrefix, IQueryDispatcher queries, CancellationToken cancellationToken)
                => (await queries.DispatchAsync(new SearchCatalogQuery(formPrefix), cancellationToken)
                    .ConfigureAwait(false)).ToHttpResult());

        return routes;
    }
}
