using FluentValidation;

namespace LibraryManagement.Circulation.Application.Holds.CancelHoldsForDebt;

/// <summary>
/// Checks the shape of a <see cref="CancelHoldsForDebtCommand"/>.
/// </summary>
public sealed class CancelHoldsForDebtCommandValidator
    : AbstractValidator<CancelHoldsForDebtCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CancelHoldsForDebtCommandValidator"/> class.
    /// </summary>
    public CancelHoldsForDebtCommandValidator()
    {
        RuleFor(command => command.BorrowerId)
            .NotEmpty()
            .WithMessage("A borrower identifier is required.");
    }
}
