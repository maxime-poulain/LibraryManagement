using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.WithdrawCopy;

/// <summary>
/// Checks the shape of a <see cref="WithdrawCopyCommand"/>.
/// </summary>
public sealed class WithdrawCopyCommandValidator : AbstractValidator<WithdrawCopyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WithdrawCopyCommandValidator"/> class.
    /// </summary>
    public WithdrawCopyCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
