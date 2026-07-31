using FluentValidation;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Authors.CorrectAuthorPreferredName;

/// <summary>
/// Checks the shape of a <see cref="CorrectAuthorPreferredNameCommand"/>.
/// </summary>
public sealed class CorrectAuthorPreferredNameCommandValidator : AbstractValidator<CorrectAuthorPreferredNameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CorrectAuthorPreferredNameCommandValidator"/> class.
    /// </summary>
    public CorrectAuthorPreferredNameCommandValidator()
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
