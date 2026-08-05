using FluentValidation;

namespace LibraryManagement.Charges.Application.Accounts.TakePayment;

/// <summary>
/// Checks the shape of a <see cref="TakePaymentCommand"/>.
/// </summary>
/// <remarks>
/// The amount is checked here as well as in the aggregate, and the two are not the same check. This
/// one refuses a negative, which <c>Money</c> cannot even represent and would throw on; the
/// aggregate refuses a payment larger than the balance, which is a rule about this member's money
/// and not about the shape of the request.
/// </remarks>
public sealed class TakePaymentCommandValidator : AbstractValidator<TakePaymentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TakePaymentCommandValidator"/> class.
    /// </summary>
    public TakePaymentCommandValidator()
    {
        RuleFor(command => command.MemberId)
            .NotEmpty()
            .WithMessage("A member identifier is required.");

        RuleFor(command => command.Amount)
            .GreaterThan(0m)
            .WithMessage("A payment must be for more than nothing.");
    }
}
