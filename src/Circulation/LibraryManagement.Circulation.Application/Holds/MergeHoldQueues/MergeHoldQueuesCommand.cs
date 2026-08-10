using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.MergeHoldQueues;

/// <summary>
/// Makes the queue of an absorbed catalog record and the queue of its survivor into one.
/// </summary>
/// <param name="AbsorbedEditionId">The identifier that stopped naming a record of its own.</param>
/// <param name="SurvivingEditionId">The identifier the merged queue is keyed by.</param>
/// <remarks>
/// <para>
/// Reached from a fact Catalog announced and never from the desk, which is why it is not routed:
/// nobody is standing at the counter when a cataloger, elsewhere, judges two records to describe one
/// edition.
/// </para>
/// <para>
/// The expensive half of what a merge costs this context. Its sibling
/// <c>RepointLoansOfMergedEditionCommand</c> moves a field; this one makes two aggregates into one,
/// which is the whole reason ADR-0017 was written before either was built.
/// </para>
/// </remarks>
public sealed record MergeHoldQueuesCommand(
    Guid AbsorbedEditionId,
    Guid SurvivingEditionId) : ICommand<Result>;
