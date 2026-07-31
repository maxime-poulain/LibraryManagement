using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.ReturnCopyFromRepair;

/// <summary>
/// Brings a copy back from repair.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <remarks>
/// It returns to whatever it left, not to the lending stock by default.
/// </remarks>
public sealed record ReturnCopyFromRepairCommand(Guid CopyId) : ICommand<Result>;
