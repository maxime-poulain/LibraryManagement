using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.PlaceHold;

/// <summary>
/// Places a claim on the next available copy of an edition.
/// </summary>
/// <param name="HoldId">The identifier the claim will keep, into the history that outlives it.</param>
/// <param name="EditionId">The edition claimed — never a copy: a borrower waiting for a title
/// wants the next copy, not a particular volume.</param>
/// <param name="BorrowerId">Whose claim it is.</param>
public sealed record PlaceHoldCommand(Guid HoldId, Guid EditionId, Guid BorrowerId) : ICommand<Result>;
