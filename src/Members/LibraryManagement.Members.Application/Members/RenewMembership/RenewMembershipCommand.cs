using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.RenewMembership;

/// <summary>
/// Renews a member's membership.
/// </summary>
/// <param name="MemberId">The member renewing.</param>
/// <remarks>
/// Always granted — nothing refuses a renewal. A member returning after three years renews too:
/// the identity persists and the loan history with it, where a re-enrollment would mint a second
/// identity and manufacture a duplicate. Renewal is also the natural moment the category is
/// reviewed, and that review is its own command, taken when the librarian takes it.
/// </remarks>
public sealed record RenewMembershipCommand(Guid MemberId) : ICommand<Result>;
