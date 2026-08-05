using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RenewLoan;

/// <summary>
/// Moves a loan's due date forward without returning the copy.
/// </summary>
/// <param name="LoanId">The loan to renew.</param>
/// <remarks>
/// Refused for three distinct reasons — the limit is reached, the borrower owes money, someone is
/// waiting — and each refusal names its own, because they call for three different things from
/// the borrower. The queue is judged as it stands at this moment: a hold placed a minute later
/// does not undo a granted renewal.
/// </remarks>
public sealed record RenewLoanCommand(Guid LoanId) : ICommand<Result>;
