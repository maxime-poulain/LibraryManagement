using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.SendCopyForRepair;

/// <summary>
/// Checks the shape of a <see cref="SendCopyForRepairCommand"/>.
/// </summary>
public sealed class SendCopyForRepairCommandValidator : AbstractValidator<SendCopyForRepairCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SendCopyForRepairCommandValidator"/> class.
    /// </summary>
    public SendCopyForRepairCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
