using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.TakePayment;

/// <summary>
/// Records money received against what a member owes.
/// </summary>
/// <param name="MemberId">Who paid.</param>
/// <param name="Amount">How much was received.</param>
/// <remarks>
/// <para>
/// A desk moment, unlike the two charges above: a member hands over money and a librarian records
/// it. It settles the oldest charge first and does not name one — naming a charge is an addition the
/// day the desk asks for it, not a second concept.
/// </para>
/// <para>
/// Nothing here says how the money arrived. Cash, card, the drawer it went into and the close of day
/// it is reconciled against are a point-of-sale concern, and this command is where the two would one
/// day meet.
/// </para>
/// </remarks>
public sealed record TakePaymentCommand(Guid MemberId, decimal Amount) : ICommand<Result>;
