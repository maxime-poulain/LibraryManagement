using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.ReturnCopy;

/// <summary>
/// Ends a loan by the copy coming back — the moment that justifies holds and loans living in one
/// context.
/// </summary>
/// <param name="CopyId">The copy in hand. The desk scans an object, not a loan, so the object
/// names the command and the active loan is found from it.</param>
/// <remarks>
/// A borrower may always return: no standing, no cap, no queue is consulted on the way in. In one
/// transaction the loan closes, lateness is computed and recorded, and the edition's queue is
/// asked whether the copy is wanted — trapped for the oldest claim in good standing, or back to
/// the shelf.
/// </remarks>
public sealed record ReturnCopyCommand(Guid CopyId) : ICommand<Result>;
