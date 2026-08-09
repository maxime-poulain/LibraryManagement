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
/// It reads the table rather than loading the aggregate: the questions want a row and a column or
/// two, and <c>Copy.MayBeLent</c> is the same rule written where the domain can state it. The two
/// are kept in step by a test rather than by a comment, because a projection of a rule that
/// drifts from the rule is worse than no projection.
/// </remarks>
public sealed class CopyLendability(HoldingsDbContext context) : ICopyLendability
{
    /// <inheritdoc/>
    public async ValueTask<LendabilityAnswer> OfAsync(
        Guid copyId,
        CancellationToken cancellationToken = default)
    {
        // Built into the module's own identifier first: the key carries a value conversion, so a
        // comparison on `Id.Value` does not translate — the provider sees a member access on a type
        // it has no column for.
        var id = CopyId.Create(copyId);

        var found = await context.Copies
            .Where(copy => copy.Id == id)
            .Select(copy => new { copy.Status, copy.EditionId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Three answers, and the third is not a refusal. A barcode matching nothing means a mis-scan
        // or a copy never accessioned, and a librarian handles that differently from a copy in
        // repair.
        return found switch
        {
            null => new LendabilityAnswer(Lendability.NoSuchCopy, EditionId: null),
            { Status: CopyStatus.InService }
                => new LendabilityAnswer(Lendability.Lendable, found.EditionId.Value),
            _ => new LendabilityAnswer(Lendability.NotLendable, found.EditionId.Value),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Guid>> LendableCopiesOfAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
    {
        var id = EditionId.Create(editionId);

        return await context.Copies
            .Where(copy => copy.EditionId == id && copy.Status == CopyStatus.InService)
            .Select(copy => copy.Id.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> AnyCopyExpectedToServeAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
    {
        var id = EditionId.Create(editionId);

        // In service, or away in repair and expected back in it. Withdrawn and lost copies serve
        // nobody, and a reference-only copy is held rather than lent — none of the three is a
        // 'next available copy' a claim could wait for.
        return await context.Copies
            .AnyAsync(
                copy => copy.EditionId == id
                    && (copy.Status == CopyStatus.InService || copy.Status == CopyStatus.InRepair),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
