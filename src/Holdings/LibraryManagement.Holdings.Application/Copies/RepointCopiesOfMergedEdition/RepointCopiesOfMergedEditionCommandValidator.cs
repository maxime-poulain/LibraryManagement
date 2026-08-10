using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.RepointCopiesOfMergedEdition;

/// <summary>
/// Checks the shape of a <see cref="RepointCopiesOfMergedEditionCommand"/>.
/// </summary>
public sealed class RepointCopiesOfMergedEditionCommandValidator
    : AbstractValidator<RepointCopiesOfMergedEditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepointCopiesOfMergedEditionCommandValidator"/>
    /// class.
    /// </summary>
    public RepointCopiesOfMergedEditionCommandValidator()
    {
        RuleFor(command => command.AbsorbedEditionId)
            .NotEmpty()
            .WithMessage("An absorbed edition identifier is required.");

        RuleFor(command => command.SurvivingEditionId)
            .NotEmpty()
            .WithMessage("A surviving edition identifier is required.")
            // Catalog refuses a record merged into itself before anything is announced. Repeating the
            // check here is not distrust of that module: a command carries its own shape, and one
            // that named the same edition twice would sweep a whole edition's copies to repoint them
            // where they already are.
            .NotEqual(command => command.AbsorbedEditionId)
            .WithMessage("A record cannot survive its own merge.");
    }
}
