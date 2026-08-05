using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.EnrollMember;

/// <summary>
/// Enrolls a member. <em>Inscription.</em>
/// </summary>
/// <param name="MemberId">The identifier the member will keep for their whole life.</param>
/// <param name="GivenName">The member's given name.</param>
/// <param name="FamilyName">The member's family name.</param>
/// <param name="DateOfBirth">The member's date of birth.</param>
/// <param name="Category">The category the librarian decided.</param>
/// <param name="CardNumber">The number of the card issued at the desk. Unique in the library.</param>
/// <param name="Email">The member's email address, if any.</param>
/// <param name="Phone">The member's phone number, if any.</param>
/// <param name="PostalAddress">The member's postal address, if any.</param>
/// <param name="Guardian">Who the member is reached through — required when the category is
/// <see cref="MemberCategory.Child"/>, available to any category, a protected adult's included.</param>
/// <remarks>
/// The first membership period starts the day the command runs; nothing here carries a date,
/// because the enrollment the business means is the one happening at the desk. Eligibility —
/// residency, the identity document presented — is desk procedure, deliberately not a model fact:
/// the model records who was enrolled, not the paperwork that satisfied the librarian.
/// </remarks>
public sealed record EnrollMemberCommand(
    Guid MemberId,
    string GivenName,
    string FamilyName,
    DateOnly DateOfBirth,
    MemberCategory Category,
    string CardNumber,
    string? Email = null,
    string? Phone = null,
    string? PostalAddress = null,
    GuardianDetails? Guardian = null) : ICommand<Result>;
