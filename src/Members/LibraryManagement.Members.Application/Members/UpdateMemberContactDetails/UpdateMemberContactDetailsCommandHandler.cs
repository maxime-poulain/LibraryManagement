using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.UpdateMemberContactDetails;

/// <summary>
/// Handles <see cref="UpdateMemberContactDetailsCommand"/>.
/// </summary>
/// <param name="members">The store holding the member.</param>
public sealed class UpdateMemberContactDetailsCommandHandler(IMemberRepository members)
    : ICommandHandler<UpdateMemberContactDetailsCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        UpdateMemberContactDetailsCommand command,
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

        return ContactDetails.Create(command.Email, command.Phone, command.PostalAddress)
            .Bind(contact =>
            {
                member.UpdateContactDetails(contact);

                return Result.Success();
            });
    }
}
