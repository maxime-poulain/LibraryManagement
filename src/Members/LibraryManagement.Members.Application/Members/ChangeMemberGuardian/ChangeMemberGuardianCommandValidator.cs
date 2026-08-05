using FluentValidation;

namespace LibraryManagement.Members.Application.Members.ChangeMemberGuardian;

/// <summary>
/// Checks the shape of a <see cref="ChangeMemberGuardianCommand"/>.
/// </summary>
public sealed class ChangeMemberGuardianCommandValidator
    : AbstractValidator<ChangeMemberGuardianCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeMemberGuardianCommandValidator"/> class.
    /// </summary>
    public ChangeMemberGuardianCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");

        // A null guardian is a legitimate command — the removal — so only a carried one has a
        // shape to check. Whether removing is *allowed* is the aggregate's rule.
        RuleFor(command => command.Guardian!)
            .SetValidator(new GuardianDetailsValidator())
            .When(command => command.Guardian is not null);
    }
}
