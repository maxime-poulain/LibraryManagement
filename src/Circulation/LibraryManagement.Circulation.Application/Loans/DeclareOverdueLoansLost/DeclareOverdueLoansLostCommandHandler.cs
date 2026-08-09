using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.DeclareOverdueLoansLost;

/// <summary>
/// Handles <see cref="DeclareOverdueLoansLostCommand"/>.
/// </summary>
/// <param name="loans">Every active loan past its due date; the aggregate says which have waited
/// long enough.</param>
/// <param name="policy">The circulation policy, which holds how long the library waits.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// Refusals are collected rather than ignored, though the filter should make them impossible: a
/// loan that refuses here means the query and the aggregate disagree about what active means, and
/// a batch that swallowed that would hide the disagreement forever. The whole run then fails and
/// writes nothing, which costs nothing — the next run declares the same loans.
/// </remarks>
public sealed class DeclareOverdueLoansLostCommandHandler(
    ILoanRepository loans,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<DeclareOverdueLoansLostCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        DeclareOverdueLoansLostCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var today = clock.Today();

        var overdue = await loans.ActiveOverdueAsync(today, cancellationToken)
            .ConfigureAwait(false);

        var errors = new ErrorCollection();

        foreach (var loan in overdue.Where(loan => loan.IsLongOverdue(today, policy)))
        {
            loan.DeclareLost(today).TapError(errors.AddErrors);
        }

        return errors.Count > 0 ? Result.Failure(errors) : Result.Success();
    }
}
