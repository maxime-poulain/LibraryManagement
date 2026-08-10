using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Editions;

/// <summary>
/// The rules that decide whether two records may be joined, and the act of joining them.
/// </summary>
/// <remarks>
/// <para>
/// <strong>All four rules are here, and that is the point.</strong> Two of them are about one
/// record alone — it cannot absorb itself, and a record already absorbed no longer acts — and two
/// are about the pair: the survivor must itself be live, and both must print the same work. An
/// earlier version put the first two on the aggregate and the last two in the command handler,
/// which left half a merge in the application layer and half a guard on the aggregate. Rules that
/// answer one question belong in one place, or they drift apart.
/// </para>
/// <para>
/// <strong>Both records are read; only one is written.</strong> The survivor is consulted and never
/// changed, so this stays one aggregate's transaction — the unit of consistency the command already
/// was. Nothing here saves, and nothing here reaches a store.
/// </para>
/// <para>
/// <strong>Every refusal is reported, not just the first.</strong> A cataloger looking at two
/// records that turn out to print different works, one of which was already merged away, is better
/// served learning both than making two trips.
/// </para>
/// </remarks>
public sealed class EditionMergeDomainService : IEditionMergeDomainService
{
    /// <inheritdoc/>
    public Result Merge(Edition absorbed, Edition surviving)
    {
        ArgumentNullException.ThrowIfNull(absorbed);
        ArgumentNullException.ThrowIfNull(surviving);

        var errors = new ErrorCollection();

        if (absorbed.Id == surviving.Id)
        {
            // Checked first and alone: every rule below reads as nonsense about one record
            // compared with itself, and reporting them would describe a mistake nobody made.
            return Result.Failure(
                CatalogErrorCodes.EditionCannotAbsorbItself,
                "An edition cannot be merged into itself.");
        }

        if (absorbed.AbsorbedInto is not null)
        {
            // Not idempotent-by-silence, deliberately. A second merge of the same record is either
            // a mistake or a chain — and a chain is what would leave a downstream identifier
            // pointing at a pointer. Whoever meant it merges the survivor instead.
            errors.Add(
                CatalogErrorCodes.EditionAlreadyAbsorbed,
                $"Edition '{absorbed.Id}' was already merged into '{absorbed.AbsorbedInto}'.");
        }

        if (surviving.AbsorbedInto is not null)
        {
            errors.Add(
                CatalogErrorCodes.EditionAlreadyAbsorbed,
                $"The surviving edition '{surviving.Id}' was itself merged into "
                + $"'{surviving.AbsorbedInto}', so it cannot absorb another.");
        }

        if (absorbed.WorkId != surviving.WorkId)
        {
            // The guard against the one mistake nothing recovers. Merging these tells Holdings to
            // repoint copies onto an edition of another book, after which the copies have to be
            // told apart by hand and nothing recorded which ones moved. Where a cataloger genuinely
            // has two records for one *work*, the work is what they merge.
            errors.Add(
                CatalogErrorCodes.EditionsPrintDifferentWorks,
                $"Edition '{absorbed.Id}' prints work '{absorbed.WorkId}' and '{surviving.Id}' "
                + $"prints '{surviving.WorkId}'; editions of different works are not duplicates.");
        }

        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        absorbed.AbsorbInto(surviving.Id);

        return Result.Success();
    }
}
