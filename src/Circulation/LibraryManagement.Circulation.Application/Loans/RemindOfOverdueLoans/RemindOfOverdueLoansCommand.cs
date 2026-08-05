using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RemindOfOverdueLoans;

/// <summary>
/// Tells the borrowers whose copies are late, once per stage of the schedule.
/// </summary>
/// <remarks>
/// A batch like its courtesy counterpart, and idempotent for the same reason: each loan records
/// the stages it has already announced, so a second run of the day says nothing.
/// </remarks>
public sealed record RemindOfOverdueLoansCommand : ICommand<Result>;
