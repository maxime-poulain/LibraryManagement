using FluentValidation;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorHeading;

/// <summary>
/// Checks the shape of a <see cref="CorrectAuthorHeadingCommand"/>.
/// </summary>
public sealed class CorrectAuthorHeadingCommandValidator : AbstractValidator<CorrectAuthorHeadingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CorrectAuthorHeadingCommandValidator"/> class.
    /// </summary>
    public CorrectAuthorHeadingCommandValidator()
    {
        RuleFor(command => command.AuthorId)
            .NotEmpty()
            .WithMessage("An author identifier is required.");

        RuleFor(command => command.CorrectedName)
            .NotEmpty()
            .WithMessage("A corrected name is required.")
            .MaximumLength(NameForm.MaxLength)
            .WithMessage($"A corrected name may not exceed {NameForm.MaxLength} characters.");
    }
}
