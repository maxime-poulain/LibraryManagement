using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedBorrower;

/// <summary>
/// Handles <see cref="RepointLoansOfMergedBorrowerCommand"/>.
/// </summary>
/// <param name="loans">The loans the absorbed record has not yet answered for.</param>
/// <remarks>
/// <para>
/// <strong>It succeeds when there is nothing to repoint.</strong> A member with nothing out and
/// nothing written off is the ordinary case, and this arrives from another module's drain, where a
/// refusal would make that drain replay a message it handled correctly, forever.
/// </para>
/// <para>
/// <strong>Redelivery costs nothing and needs no deduplication table.</strong> On a replay the
/// absorbed identifier answers for no unsettled loan any more, so the sweep returns nothing. The
/// idempotence is a property of the question asked rather than a mark kept somewhere — the same
/// property the edition sweep relies on.
/// </para>
/// <para>
/// <strong>It does not ask Members whether the survivor exists</strong>, for the reason the edition
/// sweep does not ask Catalog: the fact came from the module that owns the answer, and a survivor
/// merged again between the announcement and the drain answers <em>no</em> — asking would open a
/// race rather than close one. Ordering already resolves the chain.
/// </para>
/// <para>
/// No domain service: each loan changes its own field, independently of the others. This is a
/// sweep, not a judgement.
/// </para>
/// </remarks>
public sealed class RepointLoansOfMergedBorrowerCommandHandler(ILoanRepository loans)
    : ICommandHandler<RepointLoansOfMergedBorrowerCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RepointLoansOfMergedBorrowerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var absorbed = BorrowerId.Create(command.AbsorbedBorrowerId);
        var surviving = BorrowerId.Create(command.SurvivingBorrowerId);

        var affected = await loans.NotYetAnsweredForByBorrowerAsync(absorbed, cancellationToken)
            .ConfigureAwait(false);

        var errors = new ErrorCollection();

        foreach (var loan in affected)
        {
            errors.AddErrors(loan.RepointBorrowerTo(surviving));
        }

        return errors.Count > 0 ? Result.Failure(errors) : Result.Success();
    }
}
