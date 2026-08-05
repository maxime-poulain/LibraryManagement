using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.ChangeMemberCategory;

/// <summary>
/// Moves a member to another category.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="Category">The category from now on.</param>
/// <remarks>
/// A decision at the desk, any time — renewal is the ordinary moment, not the only one. Age is a
/// fact and category is a decision: nothing in the system changes a category at midnight on a
/// birthday, and this command is where the librarian does.
/// </remarks>
public sealed record ChangeMemberCategoryCommand(
    Guid MemberId,
    MemberCategory Category) : ICommand<Result>;
