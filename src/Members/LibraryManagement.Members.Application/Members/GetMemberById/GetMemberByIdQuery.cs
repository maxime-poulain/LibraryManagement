using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Members.Application.Members.GetMemberById;

/// <summary>
/// Reads one member by identity.
/// </summary>
/// <param name="MemberId">The member to read.</param>
/// <remarks>
/// The question this module answers for a page it does not compose. A member's file at the desk
/// gathers three modules' answers, and this is the first of them: who the person is. What they
/// have out and what they owe are asked of Circulation and Charges, by the composer, never by
/// this module — a module that assembled a page would have to know the other two exist.
/// </remarks>
public sealed record GetMemberByIdQuery(Guid MemberId) : IQuery<MemberDetailsDto>;
