using FluentValidation;

namespace LibraryManagement.Circulation.Application.Loans.RecordLoanRecovery;

/// <summary>
/// Checks the shape of a <see cref="RecordLoanRecoveryCommand"/>.
/// </summary>
public sealed class RecordLoanRecoveryCommandValidator : AbstractValidator<RecordLoanRecoveryCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecordLoanRecoveryCommandValidator"/> class.
    /// </summary>
    public RecordLoanRecoveryCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
