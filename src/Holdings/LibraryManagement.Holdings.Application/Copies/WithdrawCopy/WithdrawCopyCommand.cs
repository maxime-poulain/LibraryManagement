using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.WithdrawCopy;

/// <summary>
/// Removes a copy from the collection, on purpose.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <remarks>
/// Weeding, and it is terminal: nothing that follows puts this copy back, and a copy that returned
/// would be accessioned afresh with an identity of its own.
///
/// Nothing checks that the copy is not on loan. Weeding is a shelf operation — the librarian is
/// holding the object — so the rule is satisfied physically rather than enforced, and enforcing it
/// would mean Holdings asking Circulation a question it has no business asking.
/// </remarks>
public sealed record WithdrawCopyCommand(Guid CopyId) : ICommand<Result>;
