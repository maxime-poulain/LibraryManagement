using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RemindOfOverdueLoans;

/// <summary>
/// Handles <see cref="RemindOfOverdueLoansCommand"/>.
/// </summary>
/// <param name="loans">Every active loan past its due date.</param>
/// <param name="policy">The circulation policy, which holds the stages of the schedule.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// The whole overdue set is loaded and each loan picks its own stage, rather than one query per
/// stage: three queries would read the same rows three times, and a loan that has passed two
/// stages at once must still send one message, which only the loan can decide.
/// </remarks>
public sealed class RemindOfOverdueLoansCommandHandler(
    ILoanRepository loans,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<RemindOfOverdueLoansCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RemindOfOverdueLoansCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var today = clock.Today();

        var overdue = await loans.ActiveOverdueAsync(today, cancellationToken)
            .ConfigureAwait(false);

        foreach (var loan in overdue)
        {
            loan.RemindOfBeingOverdue(today, policy);
        }

        return Result.Success();
    }
}
