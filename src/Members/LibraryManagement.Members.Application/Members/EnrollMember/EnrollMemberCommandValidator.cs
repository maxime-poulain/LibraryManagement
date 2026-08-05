using FluentValidation;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Members.EnrollMember;

/// <summary>
/// Checks the shape of an <see cref="EnrollMemberCommand"/>.
/// </summary>
public sealed class EnrollMemberCommandValidator : AbstractValidator<EnrollMemberCommand>
{
    /// <summary>
    /// The earliest date of birth a library will accept.
    /// </summary>
    /// <remarks>
    /// A fixed floor rather than a clock, for the reason <c>LifeYears</c> gives: the domain has no
    /// clock, and an implausible date is a question about the field, which a validator answers.
    /// Whether the date is in the <em>past</em> needs the day in hand, and the aggregate answers
    /// that one. Nobody enrolling in this system was born in the century before last.
    /// </remarks>
    public static readonly DateOnly EarliestBirth = new(1900, 1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="EnrollMemberCommandValidator"/> class.
    /// </summary>
    public EnrollMemberCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");

        RuleFor(command => command.GivenName)
            .NotEmpty()
            .WithMessage("A given name is required.")
            .MaximumLength(MemberName.MaxLength)
            .WithMessage($"A given name may not exceed {MemberName.MaxLength} characters.");

        RuleFor(command => command.FamilyName)
            .NotEmpty()
            .WithMessage("A family name is required.")
            .MaximumLength(MemberName.MaxLength)
            .WithMessage($"A family name may not exceed {MemberName.MaxLength} characters.");

        RuleFor(command => command.DateOfBirth)
            .GreaterThanOrEqualTo(EarliestBirth)
            .WithMessage($"A date of birth may not precede {EarliestBirth:yyyy-MM-dd}.");

        RuleFor(command => command.Category)
            .IsInEnum()
            .WithMessage("A category must be one of adult, child or student.");

        RuleFor(command => command.CardNumber)
            .NotEmpty()
            .WithMessage("A card number is required.")
            .MinimumLength(CardNumber.MinLength)
            .WithMessage($"A card number must be at least {CardNumber.MinLength} characters.")
            .MaximumLength(CardNumber.MaxLength)
            .WithMessage($"A card number may not exceed {CardNumber.MaxLength} characters.");

        RuleFor(command => command.Email)
            .MaximumLength(ContactDetails.MaxEmailLength)
            .WithMessage($"An email address may not exceed {ContactDetails.MaxEmailLength} characters.");

        RuleFor(command => command.Phone)
            .MaximumLength(ContactDetails.MaxPhoneLength)
            .WithMessage($"A phone number may not exceed {ContactDetails.MaxPhoneLength} characters.");

        RuleFor(command => command.PostalAddress)
            .MaximumLength(ContactDetails.MaxPostalAddressLength)
            .WithMessage($"A postal address may not exceed {ContactDetails.MaxPostalAddressLength} characters.");

        // Skipped when no guardian is carried; whether one is *required* is the aggregate's rule,
        // judged against the category, not a shape question.
        RuleFor(command => command.Guardian!)
            .SetValidator(new GuardianDetailsValidator())
            .When(command => command.Guardian is not null);
    }
}
