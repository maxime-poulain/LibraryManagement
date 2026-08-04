using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.ChangeMemberGuardian;

/// <summary>
/// Records who a member is reached through, or that they now are reached directly.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="Guardian">The guardian from now on, or <see langword="null"/> to remove one.</param>
/// <remarks>
/// An ordinary correction — a fact about how a person is reached, not about who they are. One
/// guard, and it is the aggregate's: removing the guardian of a child is refused, because the
/// operation that legitimately ends a child's guardianship is the category change, not a deletion
/// that would leave a minor unreachable.
/// </remarks>
public sealed record ChangeMemberGuardianCommand(
    Guid MemberId,
    GuardianDetails? Guardian) : ICommand<Result>;
