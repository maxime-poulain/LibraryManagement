using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.ChangeMemberCategory;

/// <summary>
/// Handles <see cref="ChangeMemberCategoryCommand"/>.
/// </summary>
/// <param name="members">The store holding the member.</param>
public sealed class ChangeMemberCategoryCommandHandler(IMemberRepository members)
    : ICommandHandler<ChangeMemberCategoryCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        ChangeMemberCategoryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var memberId = MemberId.Create(command.MemberId);
        var member = await members.GetByIdAsync(memberId, cancellationToken).ConfigureAwait(false);

        if (member is null)
        {
            return Result.Failure(
                MembersErrorCodes.MemberNotFound,
                $"Nobody is enrolled under '{memberId}'.");
        }

        return member.ChangeCategory(command.Category);
    }
}
