using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelHoldsOfOwingBorrowers;

/// <summary>
/// Sweeps the queues for borrowers whose debt forbids the places they still hold, and cancels
/// them — the daily reconciliation behind the debt event.
/// </summary>
/// <remarks>
/// A reconciliation, never the first line of defence. The debt event does this work the day it
/// arrives; this sweep exists because a message can die on the drain's floor after its five
/// attempts, and the crossing is announced only once — a convergence with no reconciliation is a
/// convergence that can silently stop converging. On any day every event arrived, which is every
/// ordinary day, the sweep finds nothing.
/// </remarks>
public sealed record CancelHoldsOfOwingBorrowersCommand : ICommand<Result>;
