using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.FindCopy;

/// <summary>
/// Records that a copy nobody could account for has turned up.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <remarks>
/// This is what makes a loss differ in kind from a withdrawal. Copies turn up — reshelved two rows
/// down, returned in a book drop months later — and a model whose only route out of a loss is
/// someone editing the database teaches its users to distrust it. The command names no
/// destination: the copy returns to the status it was lost from, which the aggregate remembered
/// when it was declared, and a finder who judges it belongs elsewhere corrects that afterwards
/// with the ordinary restriction and release moments.
/// </remarks>
public sealed record FindCopyCommand(Guid CopyId) : ICommand<Result>;
