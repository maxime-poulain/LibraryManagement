using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.RestrictCopyToReference;

/// <summary>
/// Holds a copy back from lending: consultable on site, never lent.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <remarks>
/// A curatorial decision, and a reversible one — <c>ReleaseCopyForLendingCommand</c> is its undo.
/// </remarks>
public sealed record RestrictCopyToReferenceCommand(Guid CopyId) : ICommand<Result>;
