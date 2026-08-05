using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Members.Domain;

/// <summary>
/// Every <see cref="ErrorCode"/> the Members context can produce.
/// </summary>
/// <remarks>
/// Declared here rather than in the shared kernel, and prefixed with the context's own name, so no
/// two contexts can claim the same code and a code is self-describing wherever it surfaces.
/// </remarks>
public static class MembersErrorCodes
{
    /// <summary>A name is blank, or a part of it runs longer than a name may.</summary>
    public static readonly ErrorCode InvalidMemberName = new("Members.InvalidMemberName");

    /// <summary>A card number is empty, blank, or outside the length a card may carry.</summary>
    public static readonly ErrorCode InvalidCardNumber = new("Members.InvalidCardNumber");

    /// <summary>A contact channel runs longer than its column will hold.</summary>
    public static readonly ErrorCode InvalidContactDetails = new("Members.InvalidContactDetails");

    /// <summary>The card number is already assigned. It identifies one member in the library.</summary>
    public static readonly ErrorCode CardNumberAlreadyInUse = new("Members.CardNumberAlreadyInUse");

    /// <summary>No member is enrolled under that identifier.</summary>
    public static readonly ErrorCode MemberNotFound = new("Members.MemberNotFound");

    /// <summary>
    /// A child member must have a guardian, always — the invariant holds at enrollment, at a
    /// category change into <c>Child</c>, and against a removal that would leave a minor
    /// unreachable.
    /// </summary>
    public static readonly ErrorCode GuardianRequired = new("Members.GuardianRequired");

    /// <summary>
    /// A date of birth must be in the past. The rest of a date's plausibility is the validator's
    /// question; this one needs the day in hand, so the aggregate answers it.
    /// </summary>
    public static readonly ErrorCode DateOfBirthNotInThePast = new("Members.DateOfBirthNotInThePast");
}
