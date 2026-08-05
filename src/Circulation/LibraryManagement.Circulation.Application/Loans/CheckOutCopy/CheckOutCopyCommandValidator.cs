using FluentValidation;

namespace LibraryManagement.Circulation.Application.Loans.CheckOutCopy;

/// <summary>
/// Checks the shape of a <see cref="CheckOutCopyCommand"/>.
/// </summary>
public sealed class CheckOutCopyCommandValidator : AbstractValidator<CheckOutCopyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckOutCopyCommandValidator"/> class.
    /// </summary>
    public CheckOutCopyCommandValidator()
    {
        RuleFor(command => command.LoanId)
            .NotEmpty()
            .WithMessage("A loan identifier is required.");

        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");

        RuleFor(command => command.BorrowerId)
            .NotEmpty()
            .WithMessage("A borrower identifier is required.");
    }
}
