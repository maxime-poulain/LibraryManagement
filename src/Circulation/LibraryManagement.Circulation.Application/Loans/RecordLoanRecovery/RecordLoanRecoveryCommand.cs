using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RecordLoanRecovery;

/// <summary>
/// Records that the copy of a written-off loan has turned up, and announces what its lateness was
/// worth.
/// </summary>
/// <param name="CopyId">The copy that turned up.</param>
/// <remarks>
/// The reaction to Holdings announcing a recovery. Without it, the worst lateness cost the least:
/// a copy back on day twenty-nine owed its fine, one back on day forty-five owed nothing — the
/// replacement charge cancelled by the find, the fine never assessed because the loan ended
/// without a return. The lateness announced here froze the day the library stopped waiting, so
/// the recovered loan owes exactly what a return on that day would have owed, and never more.
/// </remarks>
public sealed record RecordLoanRecoveryCommand(Guid CopyId) : ICommand<Result>;
