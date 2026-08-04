using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.RenewMembership;

/// <summary>
/// Handles <see cref="RenewMembershipCommand"/>.
/// </summary>
/// <param name="members">The store holding the member.</param>
/// <param name="clock">The host's clock, which decides which of the two renewal arithmetics
/// applies — the aggregate owns the arithmetics, the calendar picks one.</param>
public sealed class RenewMembershipCommandHandler(IMemberRepository members, TimeProvider clock)
    : ICommandHandler<RenewMembershipCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RenewMembershipCommand command,
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

        member.Renew(clock.Today());

        return Result.Success();
    }
}
