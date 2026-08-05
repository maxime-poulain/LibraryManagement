using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelHold;

/// <summary>
/// Ends a borrower's claim on an edition because they changed their mind.
/// </summary>
/// <param name="EditionId">The edition they were queuing for.</param>
/// <param name="BorrowerId">Who changed their mind. A borrower appears at most once in a queue,
/// so the pair names the claim without a hold identifier crossing the desk.</param>
/// <remarks>
/// The ordinary exit from a queue, and it must exist: a hold occupies one of the five places the
/// cap counts, and a borrower who cannot free a place is punished for having reserved at all. No
/// penalty attaches.
/// </remarks>
public sealed record CancelHoldCommand(Guid EditionId, Guid BorrowerId) : ICommand<Result>;
