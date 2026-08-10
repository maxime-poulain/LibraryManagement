using FluentValidation;

namespace LibraryManagement.Circulation.Application.Holds.CancelHold;

/// <summary>
/// Checks the shape of a <see cref="CancelHoldCommand"/>.
/// </summary>
public sealed class CancelHoldCommandValidator : AbstractValidator<CancelHoldCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CancelHoldCommandValidator"/> class.
    /// </summary>
    public CancelHoldCommandValidator()
    {
        RuleFor(command => command.EditionId)
            .NotEmpty()
            .WithMessage("An edition identifier is required.");

        RuleFor(command => command.BorrowerId)
            .NotEmpty()
            .WithMessage("A borrower identifier is required.");

        RuleFor(command => command.HoldId)
            .NotEmpty()
            .WithMessage("A hold identifier is required.");
    }
}
