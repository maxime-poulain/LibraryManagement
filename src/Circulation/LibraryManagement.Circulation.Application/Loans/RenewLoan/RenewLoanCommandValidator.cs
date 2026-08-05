using FluentValidation;

namespace LibraryManagement.Circulation.Application.Loans.RenewLoan;

/// <summary>
/// Checks the shape of a <see cref="RenewLoanCommand"/>.
/// </summary>
public sealed class RenewLoanCommandValidator : AbstractValidator<RenewLoanCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenewLoanCommandValidator"/> class.
    /// </summary>
    public RenewLoanCommandValidator()
    {
        RuleFor(command => command.LoanId)
            .NotEmpty()
            .WithMessage("A loan identifier is required.");
    }
}
