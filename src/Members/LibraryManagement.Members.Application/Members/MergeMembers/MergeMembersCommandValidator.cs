using FluentValidation;

namespace LibraryManagement.Members.Application.Members.MergeMembers;

/// <summary>
/// Checks the shape of a <see cref="MergeMembersCommand"/>.
/// </summary>
public sealed class MergeMembersCommandValidator : AbstractValidator<MergeMembersCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MergeMembersCommandValidator"/> class.
    /// </summary>
    public MergeMembersCommandValidator()
    {
        RuleFor(command => command.AbsorbedMemberId)
            .NotEmpty()
            .WithMessage("An absorbed member identifier is required.");

        RuleFor(command => command.SurvivingMemberId)
            .NotEmpty()
            .WithMessage("A surviving member identifier is required.");
    }
}
