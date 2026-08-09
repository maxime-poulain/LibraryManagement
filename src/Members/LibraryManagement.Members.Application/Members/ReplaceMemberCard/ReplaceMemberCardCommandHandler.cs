using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.ReplaceMemberCard;

/// <summary>
/// Handles <see cref="ReplaceMemberCardCommand"/>.
/// </summary>
/// <param name="members">The store holding the member, and the only thing that can see the other
/// card numbers.</param>
/// <remarks>
/// A card number identifies one member in the library, and a member cannot see the other members
/// — so the rule spans the whole set and the handler asks the question, exactly as relabelling a
/// copy does in Holdings. It stays a business rule and not a bare constraint: the refusal names
/// the number, which a unique-index violation cannot do.
/// </remarks>
public sealed class ReplaceMemberCardCommandHandler(IMemberRepository members)
    : ICommandHandler<ReplaceMemberCardCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        ReplaceMemberCardCommand command,
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

        var cardNumber = CardNumber.Create(command.CardNumber);

        return await cardNumber.MatchAsync(
            async number =>
            {
                // Excluding this member, so replacing a card with the number it already carries
                // is not reported as a collision with itself. The aggregate treats that as a
                // no-op, and being told it collides with itself would be a lie on the way there.
                var taken = await members
                    .CardNumberIsTakenAsync(number, memberId, cancellationToken)
                    .ConfigureAwait(false);

                if (taken)
                {
                    return Result.Failure(
                        MembersErrorCodes.CardNumberAlreadyInUse,
                        $"Card number '{number}' is already assigned to another member.");
                }

                return member.ReplaceCard(number);
            },
            errors => ValueTask.FromResult(Result.Failure(errors))).ConfigureAwait(false);
    }
}
