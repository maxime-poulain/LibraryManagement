using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.ReleaseCopyForLending;

/// <summary>
/// Checks the shape of a <see cref="ReleaseCopyForLendingCommand"/>.
/// </summary>
public sealed class ReleaseCopyForLendingCommandValidator : AbstractValidator<ReleaseCopyForLendingCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseCopyForLendingCommandValidator"/> class.
    /// </summary>
    public ReleaseCopyForLendingCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
