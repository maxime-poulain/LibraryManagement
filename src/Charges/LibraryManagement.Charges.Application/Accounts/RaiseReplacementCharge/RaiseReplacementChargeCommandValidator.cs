using FluentValidation;

namespace LibraryManagement.Charges.Application.Accounts.RaiseReplacementCharge;

/// <summary>
/// Checks the shape of a <see cref="RaiseReplacementChargeCommand"/>.
/// </summary>
public sealed class RaiseReplacementChargeCommandValidator
    : AbstractValidator<RaiseReplacementChargeCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RaiseReplacementChargeCommandValidator"/> class.
    /// </summary>
    public RaiseReplacementChargeCommandValidator()
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
