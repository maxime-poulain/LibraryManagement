using FluentValidation;

namespace LibraryManagement.Charges.Application.Accounts.WaiveCharge;

/// <summary>
/// Checks the shape of a <see cref="WaiveChargeCommand"/>.
/// </summary>
public sealed class WaiveChargeCommandValidator : AbstractValidator<WaiveChargeCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WaiveChargeCommandValidator"/> class.
    /// </summary>
    public WaiveChargeCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");

        RuleFor(command => command.ChargeId)
            .NotEmpty()
            .WithMessage("A charge identifier is required.");
    }
}
