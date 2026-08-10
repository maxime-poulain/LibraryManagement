using LibraryManagement.Catalog.Domain;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Editions.MergeEditions;

/// <summary>
/// Handles <see cref="MergeEditionsCommand"/>.
/// </summary>
/// <param name="editions">The store both records are read from and the absorbed one written through.</param>
/// <param name="merge">Decides whether the two records may be joined, and joins them.</param>
/// <remarks>
/// <para>
/// Orchestration and nothing else: it asks the store the one question a store has to answer — do
/// these identifiers resolve? — and hands the two records to the domain service. Whether they may
/// be joined is four rules that need no store once both are in hand, and they live together in
/// <see cref="IEditionMergeDomainService"/> rather than half here and half on the aggregate.
/// </para>
/// <para>
/// That split is the general rule and not a preference about this use case: a question needing a
/// lookup is the handler's, a rule about aggregates already loaded is the domain's. The existence
/// check stays here for the same reason it does in
/// <see cref="RegisterEdition.RegisterEditionCommandHandler"/> — it reports *which* record is
/// missing, which a constraint violation does not.
/// </para>
/// </remarks>
public sealed class MergeEditionsCommandHandler(
    IEditionRepository editions,
    IEditionMergeDomainService merge) : ICommandHandler<MergeEditionsCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        MergeEditionsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var absorbedId = EditionId.Create(command.AbsorbedEditionId);
        var survivingId = EditionId.Create(command.SurvivingEditionId);

        var absorbed = await editions.GetByIdAsync(absorbedId, cancellationToken).ConfigureAwait(false);
        var surviving = await editions.GetByIdAsync(survivingId, cancellationToken).ConfigureAwait(false);

        var errors = new ErrorCollection();

        // Both are reported together when both are missing. A cataloger who mistyped one identifier
        // may well have mistyped the other, and finding out one at a time is two trips to the desk.
        if (absorbed is null)
        {
            errors.Add(CatalogErrorCodes.EditionNotFound, $"No edition is cataloged under '{absorbedId}'.");
        }

        if (surviving is null)
        {
            errors.Add(CatalogErrorCodes.EditionNotFound, $"No edition is cataloged under '{survivingId}'.");
        }

        return absorbed is null || surviving is null
            ? Result.Failure(errors)
            : merge.Merge(absorbed, surviving);
    }
}
