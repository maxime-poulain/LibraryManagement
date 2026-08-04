using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.UpdateMemberContactDetails;

/// <summary>
/// Records new ways of reaching a member.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="Email">The email address from now on, if any.</param>
/// <param name="Phone">The phone number from now on, if any.</param>
/// <param name="PostalAddress">The postal address from now on, if any.</param>
/// <remarks>
/// The channels replace what was recorded, wholesale: three fields are few enough that "the form
/// as refilled" is clearer at the desk than a patch language saying which of them moved. Clearing
/// every channel is legitimate — a member with none is a work-list case, not an invalid one.
/// </remarks>
public sealed record UpdateMemberContactDetailsCommand(
    Guid MemberId,
    string? Email = null,
    string? Phone = null,
    string? PostalAddress = null) : ICommand<Result>;
