using LibraryManagement.Members.Domain;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Members.EnrollMember;

/// <summary>
/// Handles <see cref="EnrollMemberCommand"/>.
/// </summary>
/// <param name="members">The store to add the new member to, and the only thing that can see the
/// other card numbers.</param>
/// <param name="clock">The host's clock. The enrollment starts the membership today, and the
/// domain has no today of its own.</param>
/// <remarks>
/// Every field-level refusal is collected before any is reported, the way an acquisition collects
/// the barcode and the edition: a librarian filling an enrollment form would rather be told about
/// the name, the card and the guardian at once than discover each after fixing the last. The two
/// rules the aggregate itself owns — the date of birth against the day in hand, the guardian a
/// child must have — are its to refuse, and arrive as a second breath when the fields were sound.
/// </remarks>
public sealed class EnrollMemberCommandHandler(IMemberRepository members, TimeProvider clock)
    : ICommandHandler<EnrollMemberCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        EnrollMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new ErrorCollection();

        var name = MemberName.Create(command.GivenName, command.FamilyName);
        name.TapError(errors.AddErrors);

        var cardNumber = CardNumber.Create(command.CardNumber);
        cardNumber.TapError(errors.AddErrors);

        var contact = ContactDetails.Create(command.Email, command.Phone, command.PostalAddress);
        contact.TapError(errors.AddErrors);

        var guardian = command.Guardian?.ToGuardian();
        guardian?.TapError(errors.AddErrors);

        // Only worth asking once the number is one: a malformed card number cannot collide with
        // anything.
        await cardNumber.MatchAsync(
            async number =>
            {
                var taken = await members
                    .CardNumberIsTakenAsync(number, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (taken)
                {
                    errors.Add(
                        MembersErrorCodes.CardNumberAlreadyInUse,
                        $"Card number '{number}' is already assigned to another member.");
                }

                return Result.Success();
            },
            _ => ValueTask.FromResult(Result.Success())).ConfigureAwait(false);

        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        return name.Combine(cardNumber, contact).Bind(parts =>
            Member.Enroll(
                    MemberId.Create(command.MemberId),
                    parts.First,
                    command.DateOfBirth,
                    command.Category,
                    parts.Second,
                    parts.Third,
                    guardian?.Match<Guardian?>(value => value, _ => null),
                    clock.Today())
                .Bind(member =>
                {
                    members.Add(member);

                    return Result.Success();
                }));
    }
}
