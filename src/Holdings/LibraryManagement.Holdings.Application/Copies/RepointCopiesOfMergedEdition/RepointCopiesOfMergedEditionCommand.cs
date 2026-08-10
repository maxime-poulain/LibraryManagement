using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.RepointCopiesOfMergedEdition;

/// <summary>
/// Files every copy of an absorbed catalog record under the record that survived the merge.
/// </summary>
/// <param name="AbsorbedEditionId">The identifier that stopped naming a record of its own.</param>
/// <param name="SurvivingEditionId">The identifier the copies hold from now on.</param>
/// <remarks>
/// <para>
/// Reached from a fact Catalog announced and never from the desk, which is why it is not routed:
/// nobody is standing at the counter when a cataloger, elsewhere, judges two records to describe one
/// edition. The desk act is Catalog's <c>MergeEditionsCommand</c>; this is what that costs here.
/// </para>
/// <para>
/// It carries two identifiers and nothing else, because the contract that triggers it carries two
/// identifiers and nothing else. Which of the two records was better described, and why one was kept,
/// is a cataloging judgement that ends where it was made.
/// </para>
/// </remarks>
public sealed record RepointCopiesOfMergedEditionCommand(
    Guid AbsorbedEditionId,
    Guid SurvivingEditionId) : ICommand<Result>;
