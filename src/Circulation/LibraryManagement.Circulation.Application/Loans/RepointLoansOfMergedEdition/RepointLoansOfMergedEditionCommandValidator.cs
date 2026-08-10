using FluentValidation;

namespace LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedEdition;

/// <summary>
/// Checks the shape of a <see cref="RepointLoansOfMergedEditionCommand"/>.
/// </summary>
public sealed class RepointLoansOfMergedEditionCommandValidator
    : AbstractValidator<RepointLoansOfMergedEditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepointLoansOfMergedEditionCommandValidator"/>
    /// class.
    /// </summary>
    public RepointLoansOfMergedEditionCommandValidator()
    {
        RuleFor(command => command.AbsorbedEditionId)
            .NotEmpty()
            .WithMessage("An absorbed edition identifier is required.");

        RuleFor(command => command.SurvivingEditionId)
            .NotEmpty()
            .WithMessage("A surviving edition identifier is required.")
            // Catalog refuses a record merged into itself before anything is announced. Repeating
            // the check here is not distrust of that module: a command answers for its own shape,
            // and one naming the same edition twice would sweep every loan of it to repoint them
            // where they already point.
            .NotEqual(command => command.AbsorbedEditionId)
            .WithMessage("A record cannot survive its own merge.");
    }
}
