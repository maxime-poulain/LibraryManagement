using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.RepointCopiesOfMergedEdition;

/// <summary>
/// Handles <see cref="RepointCopiesOfMergedEditionCommand"/>.
/// </summary>
/// <param name="copies">Every copy filed under the record that was absorbed.</param>
/// <remarks>
/// <para>
/// <strong>It succeeds when there is nothing to repoint.</strong> An edition the library holds no
/// copy of is the ordinary case — a cataloger merges records, not shelves — and this arrives from
/// another module's drain, where a refusal would make that drain replay a message it handled
/// correctly, forever.
/// </para>
/// <para>
/// <strong>Redelivery costs nothing and needs no deduplication table.</strong> On a replay the
/// absorbed identifier is on no copy any more, so the sweep returns nothing, no copy is written and
/// no event is raised a second time. The idempotence is a property of the question asked rather than
/// a mark kept somewhere.
/// </para>
/// <para>
/// <strong>It does not ask Catalog whether the survivor exists</strong>, unlike
/// <c>AcquireCopyCommandHandler</c>, which must. The fact came from the module that owns the answer,
/// and asking again would open a race rather than close one: <c>IEditionCatalog.ExistsAsync</c>
/// answers false for a record that was itself absorbed, so a survivor merged away between the
/// announcement and the drain would fail this command for good. Ordering already resolves that
/// chain — A joins B, then everything pointing at B joins C — and a check would turn a resolvable
/// sequence into a dead letter.
/// </para>
/// <para>
/// <strong>No domain service, and the convention is what says so.</strong> Nothing here is a rule
/// spanning several aggregates: each copy changes its own field, independently of the others. What
/// this handler does is a sweep, not a judgement, and a service would have nothing to decide.
/// </para>
/// </remarks>
public sealed class RepointCopiesOfMergedEditionCommandHandler(ICopyRepository copies)
    : ICommandHandler<RepointCopiesOfMergedEditionCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RepointCopiesOfMergedEditionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var absorbed = EditionId.Create(command.AbsorbedEditionId);
        var surviving = EditionId.Create(command.SurvivingEditionId);

        var affected = await copies.OfEditionAsync(absorbed, cancellationToken).ConfigureAwait(false);

        foreach (var copy in affected)
        {
            copy.RepointTo(surviving);
        }

        return Result.Success();
    }
}
