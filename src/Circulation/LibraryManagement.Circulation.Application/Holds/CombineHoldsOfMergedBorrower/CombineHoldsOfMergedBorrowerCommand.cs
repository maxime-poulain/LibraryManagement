using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CombineHoldsOfMergedBorrower;

/// <summary>
/// Combines an absorbed member record's claims with its survivor's, in every queue that holds any.
/// </summary>
/// <param name="AbsorbedBorrowerId">The identifier that stopped naming a person of its own.</param>
/// <param name="SurvivingBorrowerId">The identifier the claims answer to from now on.</param>
/// <remarks>
/// <para>
/// Reached from a fact Members announced and never from the desk, which is why it is not routed.
/// The cheap half of what a merged member costs this context, where the queue union was the
/// expensive half of a merged edition: no claim moves between aggregates and no key changes —
/// each claim changes whose it says it is, inside the queue it was always in.
/// </para>
/// <para>
/// What makes it more than a sweep is the collision the two merges share: a queue may end up
/// holding two claims of what turns out to be one person, and the survival rules ADR-0017 wrote
/// for the edition merge are applied with the words swapped — a claim awaiting pickup always
/// survives, and among the queued ones the earliest does.
/// </para>
/// </remarks>
public sealed record CombineHoldsOfMergedBorrowerCommand(
    Guid AbsorbedBorrowerId,
    Guid SurvivingBorrowerId) : ICommand<Result>;
