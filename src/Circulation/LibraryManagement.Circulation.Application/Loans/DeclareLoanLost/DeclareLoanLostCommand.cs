using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.DeclareLoanLost;

/// <summary>
/// Ends a loan at the desk because the borrower reports the copy lost.
/// </summary>
/// <param name="LoanId">The loan whose copy is gone.</param>
/// <remarks>
/// The desk road to the fact the scheduled process reaches by the clock. It exists because a
/// confession must have a door: without one, the loan runs to its thirtieth day of lateness while
/// reminders chase a copy everyone already knows is gone, and the money offered at the counter
/// cannot be taken. Always accepted, like the return — no standing, no cap — because refusing a
/// confession teaches borrowers to stop making them.
/// </remarks>
public sealed record DeclareLoanLostCommand(Guid LoanId) : ICommand<Result>;
