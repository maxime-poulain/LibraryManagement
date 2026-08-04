using FluentValidation;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Members.ReplaceMemberCard;

/// <summary>
/// Checks the shape of a <see cref="ReplaceMemberCardCommand"/>.
/// </summary>
public sealed class ReplaceMemberCardCommandValidator : AbstractValidator<ReplaceMemberCardCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceMemberCardCommandValidator"/> class.
    /// </summary>
    public ReplaceMemberCardCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");

        RuleFor(command => command.CardNumber)
            .NotEmpty()
            .WithMessage("A card number is required.")
            .MinimumLength(CardNumber.MinLength)
            .WithMessage($"A card number must be at least {CardNumber.MinLength} characters.")
            .MaximumLength(CardNumber.MaxLength)
            .WithMessage($"A card number may not exceed {CardNumber.MaxLength} characters.");
    }
}
