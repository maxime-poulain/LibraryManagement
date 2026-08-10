using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedEdition;

/// <summary>
/// Points every loan still out on an absorbed catalog record at the record that survived the merge.
/// </summary>
/// <param name="AbsorbedEditionId">The identifier that stopped naming a record of its own.</param>
/// <param name="SurvivingEditionId">The identifier the live loans hold from now on.</param>
/// <remarks>
/// <para>
/// Reached from a fact Catalog announced and never from the desk, which is why it is not routed:
/// nobody is standing at the counter when a cataloger, elsewhere, judges two records to describe one
/// edition. The desk act is Catalog's <c>MergeEditionsCommand</c>; this is part of what that costs
/// here.
/// </para>
/// <para>
/// <strong>Part, and not all of it.</strong> This context holds the merged identifier twice over —
/// in a loan's field, and as the key of a whole <c>HoldQueue</c> — and only the first is settled
/// here. Making two queues into one is a harder question with rules of its own
/// (<c>docs/adr/0017-a-merge-is-an-event-and-circulation-pays-for-it.md</c>), and it is deliberately
/// a separate change.
/// </para>
/// </remarks>
public sealed record RepointLoansOfMergedEditionCommand(
    Guid AbsorbedEditionId,
    Guid SurvivingEditionId) : ICommand<Result>;
