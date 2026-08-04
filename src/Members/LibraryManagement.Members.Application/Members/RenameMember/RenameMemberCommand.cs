using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.RenameMember;

/// <summary>
/// Records a member under a different name.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="GivenName">The given name from now on.</param>
/// <param name="FamilyName">The family name from now on.</param>
/// <remarks>
/// One command for the marriage and the typo alike. Catalog needs two operations because a
/// variant name survives a rename and not a correction; a member has no variant forms, so the
/// distinction buys nothing here.
/// </remarks>
public sealed record RenameMemberCommand(
    Guid MemberId,
    string GivenName,
    string FamilyName) : ICommand<Result>;
