using FluentValidation;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Members;

/// <summary>
/// Checks the shape of a <see cref="GuardianDetails"/>, wherever a command carries one.
/// </summary>
/// <remarks>
/// Composed into the commands' own validators with <c>SetValidator</c> rather than repeated in
/// each: the shape of a guardian is one decision, and two copies of it would drift.
/// </remarks>
public sealed class GuardianDetailsValidator : AbstractValidator<GuardianDetails>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GuardianDetailsValidator"/> class.
    /// </summary>
    public GuardianDetailsValidator()
    {
        RuleFor(guardian => guardian.GivenName)
            .NotEmpty()
            .WithMessage("A guardian's given name is required.")
            .MaximumLength(MemberName.MaxLength)
            .WithMessage($"A given name may not exceed {MemberName.MaxLength} characters.");

        RuleFor(guardian => guardian.FamilyName)
            .NotEmpty()
            .WithMessage("A guardian's family name is required.")
            .MaximumLength(MemberName.MaxLength)
            .WithMessage($"A family name may not exceed {MemberName.MaxLength} characters.");

        RuleFor(guardian => guardian.Email)
            .MaximumLength(ContactDetails.MaxEmailLength)
            .WithMessage($"An email address may not exceed {ContactDetails.MaxEmailLength} characters.");

        RuleFor(guardian => guardian.Phone)
            .MaximumLength(ContactDetails.MaxPhoneLength)
            .WithMessage($"A phone number may not exceed {ContactDetails.MaxPhoneLength} characters.");

        RuleFor(guardian => guardian.PostalAddress)
            .MaximumLength(ContactDetails.MaxPostalAddressLength)
            .WithMessage($"A postal address may not exceed {ContactDetails.MaxPostalAddressLength} characters.");
    }
}
