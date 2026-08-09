using FluentValidation;

namespace LibraryManagement.Charges.Application.Accounts.RaiseDamageCharge;

/// <summary>
/// Checks the shape of a <see cref="RaiseDamageChargeCommand"/>.
/// </summary>
public sealed class RaiseDamageChargeCommandValidator : AbstractValidator<RaiseDamageChargeCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RaiseDamageChargeCommandValidator"/> class.
    /// </summary>
    public RaiseDamageChargeCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");

        RuleFor(command => command.LoanId)
            .NotEmpty()
            .WithMessage("A loan identifier is required.");

        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
