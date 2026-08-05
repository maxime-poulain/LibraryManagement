using FluentValidation;

namespace LibraryManagement.Charges.Application.Accounts.AssessOverdueFine;

/// <summary>
/// Checks the shape of an <see cref="AssessOverdueFineCommand"/>.
/// </summary>
public sealed class AssessOverdueFineCommandValidator : AbstractValidator<AssessOverdueFineCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssessOverdueFineCommandValidator"/> class.
    /// </summary>
    public AssessOverdueFineCommandValidator()
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

        // Zero is legitimate and priced at nothing; a negative delay is a broken caller, not a late
        // return, and the message says so rather than pretending a librarian typed it.
        RuleFor(command => command.DaysLate)
            .GreaterThanOrEqualTo(0)
            .WithMessage("A return cannot be late by a negative number of days.");
    }
}
