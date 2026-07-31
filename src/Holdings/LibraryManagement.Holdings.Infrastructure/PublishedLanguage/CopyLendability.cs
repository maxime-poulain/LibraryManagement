using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Holdings.PublishedLanguage;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Holdings.Infrastructure.PublishedLanguage;

/// <summary>
/// Answers <see cref="ICopyLendability"/> from the module's own store.
/// </summary>
/// <param name="context">The module's store.</param>
/// <remarks>
/// It reads the table rather than loading the aggregate: the question wants one row and one column,
/// and <c>Copy.MayBeLent</c> is the same rule written where the domain can state it. The two are
/// kept in step by a test rather than by a comment, because a projection of a rule that drifts from
/// the rule is worse than no projection.
/// </remarks>
public sealed class CopyLendability(HoldingsDbContext context) : ICopyLendability
{
    /// <inheritdoc/>
    public async ValueTask<Lendability> OfAsync(
        Guid copyId,
        CancellationToken cancellationToken = default)
    {
        // Built into the module's own identifier first: the key carries a value conversion, so a
        // comparison on `Id.Value` does not translate — the provider sees a member access on a type
        // it has no column for.
        var id = CopyId.Create(copyId);

        var status = await context.Copies
            .Where(copy => copy.Id == id)
            .Select(copy => (CopyStatus?)copy.Status)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Three answers, and the third is not a refusal. A barcode matching nothing means a mis-scan
        // or a copy never accessioned, and a librarian handles that differently from a copy in
        // repair.
        return status switch
        {
            null => Lendability.NoSuchCopy,
            CopyStatus.InService => Lendability.Lendable,
            _ => Lendability.NotLendable,
        };
    }
}
