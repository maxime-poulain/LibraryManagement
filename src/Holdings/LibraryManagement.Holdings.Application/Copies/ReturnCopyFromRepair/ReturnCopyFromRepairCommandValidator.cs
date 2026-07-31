using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.ReturnCopyFromRepair;

/// <summary>
/// Checks the shape of a <see cref="ReturnCopyFromRepairCommand"/>.
/// </summary>
public sealed class ReturnCopyFromRepairCommandValidator : AbstractValidator<ReturnCopyFromRepairCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnCopyFromRepairCommandValidator"/> class.
    /// </summary>
    public ReturnCopyFromRepairCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
