using FluentValidation;

namespace LibraryManagement.Circulation.Application.Holds.CombineHoldsOfMergedBorrower;

/// <summary>
/// Checks the shape of a <see cref="CombineHoldsOfMergedBorrowerCommand"/>.
/// </summary>
public sealed class CombineHoldsOfMergedBorrowerCommandValidator
    : AbstractValidator<CombineHoldsOfMergedBorrowerCommand>
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="CombineHoldsOfMergedBorrowerCommandValidator"/> class.
    /// </summary>
    public CombineHoldsOfMergedBorrowerCommandValidator()
    {
        RuleFor(command => command.AbsorbedBorrowerId)
            .NotEmpty()
            .WithMessage("An absorbed borrower identifier is required.");

        RuleFor(command => command.SurvivingBorrowerId)
            .NotEmpty()
            .WithMessage("A surviving borrower identifier is required.")
            // Members refuses a record merged into itself before anything is announced. Checked
            // here too because a command answers for its own shape — and one naming the same
            // person twice would read as claims to combine what is already one file.
            .NotEqual(command => command.AbsorbedBorrowerId)
            .WithMessage("A record cannot survive its own merge.");
    }
}
