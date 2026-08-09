using FluentValidation;

namespace LibraryManagement.Circulation.Application.Loans.DeclareLoanLost;

/// <summary>
/// Checks the shape of a <see cref="DeclareLoanLostCommand"/>.
/// </summary>
public sealed class DeclareLoanLostCommandValidator : AbstractValidator<DeclareLoanLostCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeclareLoanLostCommandValidator"/> class.
    /// </summary>
    public DeclareLoanLostCommandValidator()
    {
        RuleFor(command => command.LoanId)
            .NotEmpty()
            .WithMessage("A loan identifier is required.");
    }
}
