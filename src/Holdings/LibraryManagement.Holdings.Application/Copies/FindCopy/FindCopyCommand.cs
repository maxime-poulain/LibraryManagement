using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.FindCopy;

/// <summary>
/// Records that a copy nobody could account for has turned up.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <param name="ReferenceOnly">Whether it rejoins the reference collection rather than the lending stock.</param>
/// <remarks>
/// This is what makes a loss differ in kind from a withdrawal. Copies turn up — reshelved two rows
/// down, returned in a book drop months later — and a model whose only route out of a loss is
/// someone editing the database teaches its users to distrust it.
/// </remarks>
public sealed record FindCopyCommand(Guid CopyId, bool ReferenceOnly = false) : ICommand<Result>;
