using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.DeclareCopyLost;

/// <summary>
/// Records that a copy is unaccounted for.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <remarks>
/// An observation rather than a decision, which is why it can be undone by <c>FindCopyCommand</c> and
/// why a withdrawal cannot. This is also the route a stocktake will use the day it is modeled.
/// </remarks>
public sealed record DeclareCopyLostCommand(Guid CopyId) : ICommand<Result>;
