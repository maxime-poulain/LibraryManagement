using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.RecordCopyCondition;

/// <summary>
/// Records a copy's physical state.
/// </summary>
/// <param name="CopyId">The copy.</param>
/// <param name="Condition">The state as observed.</param>
/// <remarks>
/// Condition decides nothing today: a worn copy is perfectly lendable, and most of a public
/// library's stock is worn. It is recorded because staff record it, and because the decision to
/// repair or to weed is argued from it.
/// </remarks>
public sealed record RecordCopyConditionCommand(Guid CopyId, CopyCondition Condition)
    : ICommand<Result>;
