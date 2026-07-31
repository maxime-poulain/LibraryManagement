using FluentValidation;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Application.Copies.ReshelveCopy;

/// <summary>
/// Checks the shape of a <see cref="ReshelveCopyCommand"/>.
/// </summary>
public sealed class ReshelveCopyCommandValidator : AbstractValidator<ReshelveCopyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReshelveCopyCommandValidator"/> class.
    /// </summary>
    public ReshelveCopyCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");

        RuleFor(command => command.Shelfmark)
            .NotEmpty()
            .WithMessage("A shelfmark is required.")
            .MaximumLength(Shelfmark.MaxLength)
            .WithMessage($"A shelfmark may not exceed {Shelfmark.MaxLength} characters.");
    }
}
