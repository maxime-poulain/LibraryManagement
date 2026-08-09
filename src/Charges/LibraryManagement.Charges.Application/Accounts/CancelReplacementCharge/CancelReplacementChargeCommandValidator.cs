using FluentValidation;

namespace LibraryManagement.Charges.Application.Accounts.CancelReplacementCharge;

/// <summary>
/// Checks the shape of a <see cref="CancelReplacementChargeCommand"/>.
/// </summary>
public sealed class CancelReplacementChargeCommandValidator
    : AbstractValidator<CancelReplacementChargeCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CancelReplacementChargeCommandValidator"/> class.
    /// </summary>
    public CancelReplacementChargeCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
