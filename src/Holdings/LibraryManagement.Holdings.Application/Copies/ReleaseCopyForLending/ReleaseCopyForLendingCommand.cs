using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.ReleaseCopyForLending;

/// <summary>
/// Returns a copy that was held back to the lending stock.
/// </summary>
/// <param name="CopyId">The copy.</param>
public sealed record ReleaseCopyForLendingCommand(Guid CopyId) : ICommand<Result>;
