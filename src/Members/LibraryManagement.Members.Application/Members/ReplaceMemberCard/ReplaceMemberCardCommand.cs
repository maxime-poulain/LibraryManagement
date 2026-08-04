using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.ReplaceMemberCard;

/// <summary>
/// Replaces a member's card.
/// </summary>
/// <param name="MemberId">The member. Their identity does not change, which is the point.</param>
/// <param name="CardNumber">The number from now on.</param>
/// <remarks>
/// An ordinary afternoon's work at a service desk: cards are lost, chewed and demagnetized. The
/// card number is a current fact about the member and never their identity — were it the
/// identity, a replacement would produce a different member and orphan every loan ever made
/// against the old one.
/// </remarks>
public sealed record ReplaceMemberCardCommand(Guid MemberId, string CardNumber) : ICommand<Result>;
