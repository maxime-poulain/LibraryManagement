using FluentValidation;

namespace LibraryManagement.Circulation.Application.Holds.PlaceHold;

/// <summary>
/// Checks the shape of a <see cref="PlaceHoldCommand"/>.
/// </summary>
public sealed class PlaceHoldCommandValidator : AbstractValidator<PlaceHoldCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PlaceHoldCommandValidator"/> class.
    /// </summary>
    public PlaceHoldCommandValidator()
    {
        RuleFor(command => command.HoldId)
            .NotEmpty()
            .WithMessage("A hold identifier is required.");

        RuleFor(command => command.EditionId)
            .NotEmpty()
            .WithMessage("An edition identifier is required.");

        RuleFor(command => command.BorrowerId)
            .NotEmpty()
            .WithMessage("A borrower identifier is required.");
    }
}
