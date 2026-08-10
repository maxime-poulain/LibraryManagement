using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Editions.MergeEditions;

/// <summary>
/// Merges two records for one edition: one is absorbed, the other goes on answering.
/// </summary>
/// <param name="AbsorbedEditionId">The record that stops being a record and becomes a pointer.</param>
/// <param name="SurvivingEditionId">The record that survives.</param>
/// <remarks>
/// <para>
/// The act a cataloger performs when the same book has been entered twice — the ordinary data
/// quality problem of every catalog, and the one the import will surface in bulk the day it lands
/// (§9). The caller names which record survives, because that is a judgement about which one is
/// better described and nothing in the model can make it.
/// </para>
/// <para>
/// It is routed at the desk, unlike the commands a module dispatches for another: a cataloger is
/// the one who notices the duplicate and the one entitled to say so.
/// </para>
/// </remarks>
public sealed record MergeEditionsCommand(Guid AbsorbedEditionId, Guid SurvivingEditionId)
    : ICommand<Result>;
