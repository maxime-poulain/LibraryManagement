using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.CancelReplacementCharge;

/// <summary>
/// Cancels what is still owed for a copy that has turned up.
/// </summary>
/// <param name="CopyId">The copy found.</param>
/// <remarks>
/// The second ending of a replacement charge, and the common case by some distance: a book
/// mislaid behind a radiator reappears in weeks, long before anyone has produced the replacement
/// cost at the desk. The waiver of a librarian's decision, reached by a fact from Holdings — and
/// a charge already paid is not undone here: giving money back is a concept this context
/// deliberately does not have.
/// </remarks>
public sealed record CancelReplacementChargeCommand(Guid CopyId) : ICommand<Result>;
