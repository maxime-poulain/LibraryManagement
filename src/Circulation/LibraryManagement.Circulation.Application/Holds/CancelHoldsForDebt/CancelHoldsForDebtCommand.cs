using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelHoldsForDebt;

/// <summary>
/// Ends every claim of a borrower who owes money.
/// </summary>
/// <param name="BorrowerId">Who owes.</param>
/// <remarks>
/// <para>
/// Reached from a fact Charges announced and never from the desk: nobody is standing at the counter
/// when a fine is assessed, which is why this is a command a subscriber dispatches rather than a
/// button somebody presses.
/// </para>
/// <para>
/// It carries no amount and no threshold. The amount was read and judged before this was
/// dispatched, by the policy that owns the judgement; what arrives here is the verdict, and this
/// context is the only place it exists.
/// </para>
/// </remarks>
public sealed record CancelHoldsForDebtCommand(Guid BorrowerId) : ICommand<Result>;
