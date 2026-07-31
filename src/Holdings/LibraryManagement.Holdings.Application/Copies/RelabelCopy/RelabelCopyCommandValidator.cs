using FluentValidation;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Application.Copies.RelabelCopy;

/// <summary>
/// Checks the shape of a <see cref="RelabelCopyCommand"/>.
/// </summary>
public sealed class RelabelCopyCommandValidator : AbstractValidator<RelabelCopyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelabelCopyCommandValidator"/> class.
    /// </summary>
    public RelabelCopyCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");

        RuleFor(command => command.Barcode)
            .NotEmpty()
            .WithMessage("A barcode is required.")
            .MinimumLength(Barcode.MinLength)
            .WithMessage($"A barcode must be at least {Barcode.MinLength} characters.")
            .MaximumLength(Barcode.MaxLength)
            .WithMessage($"A barcode may not exceed {Barcode.MaxLength} characters.");
    }
}
