using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelHold;

/// <summary>
/// Ends a borrower's claim on an edition because they changed their mind.
/// </summary>
/// <param name="EditionId">The edition they were queuing for.</param>
/// <param name="BorrowerId">Who changed their mind, and whose claim it must be.</param>
/// <param name="HoldId">Which claim to end.</param>
/// <remarks>
/// <para>
/// The ordinary exit from a queue, and it must exist: a hold occupies one of the five places the
/// cap counts, and a borrower who cannot free a place is punished for having reserved at all. No
/// penalty attaches.
/// </para>
/// <para>
/// <strong>The claim is named, and it did not use to be.</strong> The pair of edition and borrower
/// identified it for as long as a borrower could appear only once in a queue; merging two queues
/// ends that, and the aggregate would then have had to pick one of two claims and would have picked
/// silently. The borrower travels alongside so that naming somebody else's claim cannot end it.
/// </para>
/// <para>
/// The desk has the identifier already: the borrower's file lists each claim with its
/// <c>HoldId</c>, which is the screen a cancellation is asked from.
/// </para>
/// </remarks>
public sealed record CancelHoldCommand(Guid EditionId, Guid BorrowerId, Guid HoldId)
    : ICommand<Result>;
