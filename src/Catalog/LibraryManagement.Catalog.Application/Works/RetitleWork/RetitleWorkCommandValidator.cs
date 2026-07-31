using FluentValidation;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Application.Works.RetitleWork;

/// <summary>
/// Checks the shape of a <see cref="RetitleWorkCommand"/>.
/// </summary>
public sealed class RetitleWorkCommandValidator : AbstractValidator<RetitleWorkCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetitleWorkCommandValidator"/> class.
    /// </summary>
    public RetitleWorkCommandValidator()
    {
        RuleFor(command => command.WorkId)
            .NotEmpty()
            .WithMessage("A work identifier is required.");

        RuleFor(command => command.NewPreferredTitle)
            .NotEmpty()
            .WithMessage("A title is required.")
            .MaximumLength(Title.MaxLength)
            .WithMessage($"A title may not exceed {Title.MaxLength} characters.");
    }
}
