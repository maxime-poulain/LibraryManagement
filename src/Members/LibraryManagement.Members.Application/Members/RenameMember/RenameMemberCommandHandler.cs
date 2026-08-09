using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.RenameMember;

/// <summary>
/// Handles <see cref="RenameMemberCommand"/>.
/// </summary>
/// <param name="members">The store holding the member.</param>
public sealed class RenameMemberCommandHandler(IMemberRepository members)
    : ICommandHandler<RenameMemberCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RenameMemberCommand command,
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

        return MemberName.Create(command.GivenName, command.FamilyName)
            .Bind(member.Rename);
    }
}
