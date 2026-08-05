using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.WaiveCharge;

/// <summary>
/// Cancels a charge by decision rather than by payment.
/// </summary>
/// <param name="MemberId">Whose charge.</param>
/// <param name="ChargeId">The charge to cancel.</param>
/// <remarks>
/// <para>
/// A separate command from taking a payment, and separate for the reason the two events are: one is
/// money received and the other money forgone, and a library counts them apart. One command with a
/// flag would make <em>how much did we take in this month</em> a question about that flag.
/// </para>
/// <para>
/// It names one charge. Cancelling a whole balance is an amnesty, a decision at a level no account
/// can see, and it is deliberately left out — when it arrives it is a batch of these.
/// </para>
/// <para>
/// No reason is recorded, on the argument that a librarian waiving forty cents should not have to
/// write an essay. A library auditing its waivers would want one, and the field is an addition —
/// but the day it becomes mandatory it changes what this operation is, from a courtesy to a filing.
/// </para>
/// </remarks>
public sealed record WaiveChargeCommand(Guid MemberId, Guid ChargeId) : ICommand<Result>;
