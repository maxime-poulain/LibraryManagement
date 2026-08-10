using LibraryManagement.Members.Application.Members.ChangeMemberCategory;
using LibraryManagement.Members.Application.Members.ChangeMemberGuardian;
using LibraryManagement.Members.Application.Members.EnrollMember;
using LibraryManagement.Members.Application.Members.EraseMember;
using LibraryManagement.Members.Application.Members.MergeMembers;
using LibraryManagement.Members.Application.Members.RenameMember;
using LibraryManagement.Members.Application.Members.RenewMembership;
using LibraryManagement.Members.Application.Members.ReplaceMemberCard;
using LibraryManagement.Members.Application.Members.UpdateMemberContactDetails;

namespace LibraryManagement.Host.Http;

/// <summary>
/// The member file: who the library has enrolled, and what it may still say about them.
/// </summary>
/// <remarks>
/// Erasure is a POST like every other act and not a DELETE, because it does not delete: the record
/// survives as a terminal state holding the identifier every downstream context still carries. A
/// DELETE would promise the caller something the model refuses to do.
/// </remarks>
internal static class MembersEndpoints
{
    public static IEndpointRouteBuilder MapMembers(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGroup("/members").WithTags("Members")
            .Command<EnrollMemberCommand>("")
            .Command<RenewMembershipCommand>("/renew-membership")
            .Command<RenameMemberCommand>("/rename")
            .Command<UpdateMemberContactDetailsCommand>("/update-contact-details")
            .Command<ChangeMemberCategoryCommand>("/change-category")
            .Command<ChangeMemberGuardianCommand>("/change-guardian")
            .Command<ReplaceMemberCardCommand>("/replace-card")
            .Command<EraseMemberCommand>("/erase")
            .Command<MergeMembersCommand>("/merge");

        return routes;
    }
}
