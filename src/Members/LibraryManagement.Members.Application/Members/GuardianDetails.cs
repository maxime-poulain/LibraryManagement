using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members;

/// <summary>
/// A guardian as a command carries one: a name and the optional channels, in primitives.
/// </summary>
/// <param name="GivenName">The guardian's given name.</param>
/// <param name="FamilyName">The guardian's family name.</param>
/// <param name="Email">The guardian's email address, if any.</param>
/// <param name="Phone">The guardian's phone number, if any.</param>
/// <param name="PostalAddress">The guardian's postal address, if any.</param>
/// <remarks>
/// Shared by the two commands that record a guardian — enrolling a member and changing one — so
/// the shape of "who a minor is reached through" is declared once. Not <c>Dto</c>-suffixed on
/// purpose: that suffix is the contract of a query's answer, and this is a command's input.
/// </remarks>
public sealed record GuardianDetails(
    string GivenName,
    string FamilyName,
    string? Email = null,
    string? Phone = null,
    string? PostalAddress = null);

/// <summary>
/// Builds the domain's <see cref="Guardian"/> from the primitives a command carries.
/// </summary>
internal static class GuardianDetailsExtensions
{
    /// <summary>
    /// Creates the guardian, accumulating the name's and the channels' refusals together.
    /// </summary>
    /// <param name="details">The guardian as the command carries them.</param>
    /// <returns>The guardian, or every reason the details are not one.</returns>
    internal static Result<Guardian> ToGuardian(this GuardianDetails details)
        => MemberName.Create(details.GivenName, details.FamilyName)
            .Combine(ContactDetails.Create(details.Email, details.Phone, details.PostalAddress))
            .Bind(parts => Result<Guardian>.Success(Guardian.Of(parts.First, parts.Second)));
}
