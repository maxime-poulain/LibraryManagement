using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.DeclareLoanLost;

/// <summary>
/// Handles <see cref="DeclareLoanLostCommand"/>.
/// </summary>
/// <param name="loans">The loan the desk gives up on.</param>
/// <param name="clock">The host's clock — the day of the decision bounds what the lateness can
/// ever cost.</param>
/// <remarks>
/// No port is consulted, deliberately: like the return, this act is always accepted. Standing,
/// the cap, the queue — every gate this context keeps guards what a borrower may <em>take</em>,
/// and a loss reported is something given back, if only as a fact. Everything downstream flows
/// from the one domain event the aggregate raises, exactly as it does when the scheduled process
/// declares the same loss by the clock.
/// </remarks>
public sealed class DeclareLoanLostCommandHandler(
    ILoanRepository loans,
    TimeProvider clock) : ICommandHandler<DeclareLoanLostCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        DeclareLoanLostCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var loanId = LoanId.Create(command.LoanId);
        var loan = await loans.GetByIdAsync(loanId, cancellationToken).ConfigureAwait(false);

        if (loan is null)
        {
            return Result.Failure(
                CirculationErrorCodes.LoanNotFound,
                $"No loan is held under '{loanId}'.");
        }

        return loan.DeclareLost(clock.Today());
    }
}
