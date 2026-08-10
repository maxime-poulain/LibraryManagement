using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedEdition;

/// <summary>
/// Handles <see cref="RepointLoansOfMergedEditionCommand"/>.
/// </summary>
/// <param name="loans">The loans still out on the record that was absorbed.</param>
/// <remarks>
/// <para>
/// <strong>It succeeds when there is nothing to repoint.</strong> An edition with nothing out is the
/// ordinary case — a cataloger merges records, not shelves — and this arrives from another module's
/// drain, where a refusal would make that drain replay a message it handled correctly, forever.
/// </para>
/// <para>
/// <strong>Redelivery costs nothing and needs no deduplication table.</strong> On a replay the
/// absorbed identifier is on no live loan any more, so the sweep returns nothing, no loan is written
/// and no event is raised a second time. The idempotence is a property of the question asked rather
/// than a mark kept somewhere.
/// </para>
/// <para>
/// <strong>It does not ask Catalog whether the survivor exists.</strong> The fact came from the
/// module that owns the answer, and asking again would open a race rather than close one: a record
/// that was itself absorbed answers <em>no</em> to that question, so a survivor merged again between
/// the announcement and the drain would fail this command for good. Ordering already resolves the
/// chain — loans on A join B, then everything on B joins C.
/// </para>
/// <para>
/// <strong>Every refusal is reported, not just the first.</strong> Nothing this handler loads can
/// refuse today, since it loads live loans only; if that ever changes, one loan's objection must not
/// leave the rest of the shelf unexamined.
/// </para>
/// <para>
/// No domain service: nothing here is a rule spanning several aggregates. Each loan changes its own
/// field, independently of the others. This is a sweep, not a judgement.
/// </para>
/// </remarks>
public sealed class RepointLoansOfMergedEditionCommandHandler(ILoanRepository loans)
    : ICommandHandler<RepointLoansOfMergedEditionCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RepointLoansOfMergedEditionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var absorbed = EditionId.Create(command.AbsorbedEditionId);
        var surviving = EditionId.Create(command.SurvivingEditionId);

        var affected = await loans.ActiveOfEditionAsync(absorbed, cancellationToken)
            .ConfigureAwait(false);

        var errors = new ErrorCollection();

        foreach (var loan in affected)
        {
            errors.AddErrors(loan.RepointTo(surviving));
        }

        return errors.Count > 0 ? Result.Failure(errors) : Result.Success();
    }
}
