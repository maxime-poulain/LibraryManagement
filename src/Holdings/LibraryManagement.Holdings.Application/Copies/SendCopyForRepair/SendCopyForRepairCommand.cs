using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.SendCopyForRepair;

/// <summary>
/// Sends a copy for repair.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <remarks>
/// The copy remembers what it will come back to. A reference-only copy sent for rebinding comes back
/// reference-only, which is the whole reason the aggregate keeps that destination rather than
/// returning everything to the lending stock.
/// </remarks>
public sealed record SendCopyForRepairCommand(Guid CopyId) : ICommand<Result>;
