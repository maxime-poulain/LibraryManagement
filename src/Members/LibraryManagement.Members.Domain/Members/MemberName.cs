using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// A member's name, in ordinary order — given name then family name, what is printed on the card.
/// </summary>
/// <remarks>
/// <para>
/// A member is greeted at the desk, not filed in a catalog. <c>NameForm</c> — the filing order,
/// family name first — stays in Catalog: a member record is not an authority record, carries no
/// variant forms, and nothing here files anyone under anything.
/// </para>
/// <para>
/// The model does not parse a name beyond the split the business itself makes. Hyphens, particles,
/// apostrophes and accents are all names; a format rule would refuse a legitimate member at the
/// desk with the person standing there. Non-blankness and a length bound are the whole of it.
/// </para>
/// </remarks>
public sealed class MemberName : ValueObject<MemberName>
{
    /// <summary>The greatest number of characters either part of a name may run to.</summary>
    public const int MaxLength = 64;

    private MemberName(string givenName, string familyName)
    {
        GivenName = givenName;
        FamilyName = familyName;
    }

    /// <summary>Gets the given name.</summary>
    public string GivenName { get; }

    /// <summary>Gets the family name.</summary>
    public string FamilyName { get; }

    /// <summary>
    /// Creates a name, trimming the surrounding white space and reporting every problem at once.
    /// </summary>
    /// <param name="givenName">The given name as declared.</param>
    /// <param name="familyName">The family name as declared.</param>
    /// <returns>The name, or the reasons it is not one.</returns>
    public static Result<MemberName> Create(string? givenName, string? familyName)
    {
        var errors = new ErrorCollection();

        CheckPart(givenName, "given name", errors);
        CheckPart(familyName, "family name", errors);

        return errors.Count > 0
            ? Result<MemberName>.Failure(errors)
            : Result<MemberName>.Success(new MemberName(givenName!.Trim(), familyName!.Trim()));
    }

    private static void CheckPart(string? part, string label, ErrorCollection errors)
    {
        if (string.IsNullOrWhiteSpace(part))
        {
            errors.Add(MembersErrorCodes.InvalidMemberName, $"A {label} is required.");
        }
        else if (part.Trim().Length > MaxLength)
        {
            errors.Add(
                MembersErrorCodes.InvalidMemberName,
                $"A {label} may not exceed {MaxLength} characters.");
        }
    }

    /// <inheritdoc/>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Ordinal, both parts. A record is corrected when its case was wrong, and a correction is a
        // change — folding case here would swallow the event that announces it.
        yield return GivenName;
        yield return FamilyName;
    }

    /// <summary>Returns the name in ordinary order.</summary>
    public override string ToString() => $"{GivenName} {FamilyName}";
}
