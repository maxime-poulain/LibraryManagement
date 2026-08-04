using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.ChangeMemberGuardian;

/// <summary>
/// Handles <see cref="ChangeMemberGuardianCommand"/>.
/// </summary>
/// <param name="members">The store holding the member.</param>
public sealed class ChangeMemberGuardianCommandHandler(IMemberRepository members)
    : ICommandHandler<ChangeMemberGuardianCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        ChangeMemberGuardianCommand command,
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

        if (command.Guardian is null)
        {
            return member.ChangeGuardian(null);
        }

        return command.Guardian.ToGuardian().Bind(member.ChangeGuardian);
    }
}
