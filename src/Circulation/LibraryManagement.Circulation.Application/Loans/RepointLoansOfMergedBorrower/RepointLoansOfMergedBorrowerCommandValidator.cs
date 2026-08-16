using FluentValidation;

namespace LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedBorrower;

/// <summary>
/// Checks the shape of a <see cref="RepointLoansOfMergedBorrowerCommand"/>.
/// </summary>
public sealed class RepointLoansOfMergedBorrowerCommandValidator
    : AbstractValidator<RepointLoansOfMergedBorrowerCommand>
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="RepointLoansOfMergedBorrowerCommandValidator"/> class.
    /// </summary>
    public RepointLoansOfMergedBorrowerCommandValidator()
    {
        RuleFor(command => command.AbsorbedBorrowerId)
            .NotEmpty()
            .WithMessage("An absorbed borrower identifier is required.");

        RuleFor(command => command.SurvivingBorrowerId)
            .NotEmpty()
            .WithMessage("A surviving borrower identifier is required.")
            // Members refuses a record merged into itself before anything is announced. Repeating
            // the check here is not distrust of that module: a command answers for its own shape,
            // and one naming the same person twice would sweep their loans to repoint them where
            // they already point.
            .NotEqual(command => command.AbsorbedBorrowerId)
            .WithMessage("A record cannot survive its own merge.");
    }
}
