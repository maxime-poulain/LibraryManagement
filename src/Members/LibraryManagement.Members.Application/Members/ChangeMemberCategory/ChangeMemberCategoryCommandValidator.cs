using FluentValidation;

namespace LibraryManagement.Members.Application.Members.ChangeMemberCategory;

/// <summary>
/// Checks the shape of a <see cref="ChangeMemberCategoryCommand"/>.
/// </summary>
public sealed class ChangeMemberCategoryCommandValidator
    : AbstractValidator<ChangeMemberCategoryCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeMemberCategoryCommandValidator"/> class.
    /// </summary>
    public ChangeMemberCategoryCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");

        RuleFor(command => command.Category)
            .IsInEnum()
            .WithMessage("A category must be one of adult, child or student.");
    }
}
