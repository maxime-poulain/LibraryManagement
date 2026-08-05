using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.DeclareOverdueLoansLost;

/// <summary>
/// Stops waiting for the copies that have been out far too long.
/// </summary>
/// <remarks>
/// The end of the reminder chain, and the one moment of the scheduled process that reaches beyond
/// Circulation: <c>LoanDeclaredLost</c> is what Holdings will read to mark the copy lost and
/// Charges to price its replacement. The loan's terminal state is recorded here all the same,
/// today, because an event not published when it happened cannot be recovered afterwards.
/// </remarks>
public sealed record DeclareOverdueLoansLostCommand : ICommand<Result>;
