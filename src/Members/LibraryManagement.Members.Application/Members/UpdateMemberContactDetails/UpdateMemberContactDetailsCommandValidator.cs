using FluentValidation;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Members.UpdateMemberContactDetails;

/// <summary>
/// Checks the shape of an <see cref="UpdateMemberContactDetailsCommand"/>.
/// </summary>
public sealed class UpdateMemberContactDetailsCommandValidator
    : AbstractValidator<UpdateMemberContactDetailsCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateMemberContactDetailsCommandValidator"/>
    /// class.
    /// </summary>
    public UpdateMemberContactDetailsCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");

        RuleFor(command => command.Email)
            .MaximumLength(ContactDetails.MaxEmailLength)
            .WithMessage($"An email address may not exceed {ContactDetails.MaxEmailLength} characters.");

        RuleFor(command => command.Phone)
            .MaximumLength(ContactDetails.MaxPhoneLength)
            .WithMessage($"A phone number may not exceed {ContactDetails.MaxPhoneLength} characters.");

        RuleFor(command => command.PostalAddress)
            .MaximumLength(ContactDetails.MaxPostalAddressLength)
            .WithMessage($"A postal address may not exceed {ContactDetails.MaxPostalAddressLength} characters.");
    }
}
