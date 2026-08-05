using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.RemindOfHoldsExpiringSoon;

/// <summary>
/// Tells the borrowers whose set-aside copies are about to go back on the shelf.
/// </summary>
/// <remarks>
/// The reminder the design offers <em>instead</em> of penalising the borrower who never collects:
/// an uncollected hold immobilizes a copy for the whole pickup period, and forgetfulness is
/// almost always what causes it. Idempotent through the hold's own <c>ExpiryWarningSent</c> — the
/// loan's set of reminders answers only for loans.
/// </remarks>
public sealed record RemindOfHoldsExpiringSoonCommand : ICommand<Result>;
