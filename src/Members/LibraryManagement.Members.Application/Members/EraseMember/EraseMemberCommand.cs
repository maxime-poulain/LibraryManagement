using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.EraseMember;

/// <summary>
/// Empties a member's record at their request, keeping the identifier.
/// </summary>
/// <param name="MemberId">The member to be forgotten.</param>
/// <remarks>
/// Whether an outstanding balance should stop this is a decision at the desk, not a rule in the
/// model: the amount is on the librarian's screen, and whether a claim for money is legal grounds
/// to keep a person's record against their request is the law's question, which no aggregate can
/// answer. The model's whole job is that afterwards the identifier resolves to nobody while every
/// downstream count stays honest.
/// </remarks>
public sealed record EraseMemberCommand(Guid MemberId) : ICommand<Result>;
