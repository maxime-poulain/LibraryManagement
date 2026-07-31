using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.PublishedLanguage;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.PublishedLanguage;

/// <summary>
/// Answers <see cref="IEditionCatalog"/> from the module's own store.
/// </summary>
/// <param name="context">The module's store.</param>
/// <remarks>
/// <para>
/// It reads the table directly rather than going through <c>IEditionRepository</c>: the repository
/// hands out aggregates, and this question wants an index and no aggregate at all. The same
/// distinction <c>IAuthorRepository.ExistsAsync</c> already draws against its own <c>GetByIdAsync</c>.
/// </para>
/// <para>
/// It lives in the infrastructure because a published language is answered from the store, and
/// nothing about answering it belongs to the domain. What the domain owns is that the answer is
/// Catalog's to give — which is why the interface is declared in a project of Catalog's, and not
/// in the module that needs it.
/// </para>
/// </remarks>
public sealed class EditionCatalog(CatalogDbContext context) : IEditionCatalog
{
    /// <inheritdoc/>
    public async ValueTask<bool> ExistsAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
    {
        // Built into the module's own identifier before it reaches the query. Comparing on
        // `Id.Value` reads more directly and does not translate: the key carries a value conversion,
        // so the provider sees a member access on a type it has no column for and gives up.
        var id = EditionId.Create(editionId);

        return await context.Editions
            .AnyAsync(edition => edition.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
