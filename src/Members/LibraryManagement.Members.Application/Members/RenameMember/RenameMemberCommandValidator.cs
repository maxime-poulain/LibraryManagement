using FluentValidation;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Members.RenameMember;

/// <summary>
/// Checks the shape of a <see cref="RenameMemberCommand"/>.
/// </summary>
public sealed class RenameMemberCommandValidator : AbstractValidator<RenameMemberCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenameMemberCommandValidator"/> class.
    /// </summary>
    public RenameMemberCommandValidator()
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
    }
}
