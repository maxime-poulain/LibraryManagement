using FluentValidation;

namespace LibraryManagement.Circulation.Application.Loans.ReturnCopy;

/// <summary>
/// Checks the shape of a <see cref="ReturnCopyCommand"/>.
/// </summary>
public sealed class ReturnCopyCommandValidator : AbstractValidator<ReturnCopyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnCopyCommandValidator"/> class.
    /// </summary>
    public ReturnCopyCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
