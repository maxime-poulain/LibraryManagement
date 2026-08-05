using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.AssessOverdueFine;

/// <summary>
/// Prices a copy that came back late.
/// </summary>
/// <param name="MemberId">Who had it.</param>
/// <param name="LoanId">The loan returned.</param>
/// <param name="CopyId">The copy behind it.</param>
/// <param name="DaysLate">Days past the due date, as Circulation counted them.</param>
/// <remarks>
/// <para>
/// Reached from a fact Circulation announced and never from the desk: whether a return was late is
/// a circulation observation, and this context only prices it.
/// </para>
/// <para>
/// A return on time reaches here too, with <paramref name="DaysLate"/> at zero, and succeeds having
/// charged nothing — deciding there is nothing to charge is this context's decision, and refusing
/// the command would make the announcing module's drain retry a message that was handled correctly.
/// </para>
/// </remarks>
public sealed record AssessOverdueFineCommand(
    Guid MemberId,
    Guid LoanId,
    Guid CopyId,
    int DaysLate) : ICommand<Result>;
