using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.EraseMember;

/// <summary>
/// Handles <see cref="EraseMemberCommand"/>.
/// </summary>
/// <param name="members">The registry holding the record to empty.</param>
/// <param name="clock">The host's clock.</param>
public sealed class EraseMemberCommandHandler(
    IMemberRepository members,
    TimeProvider clock) : ICommandHandler<EraseMemberCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        EraseMemberCommand command,
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

        return member.Erase(clock.Today());
    }
}
