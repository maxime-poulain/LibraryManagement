using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.FindCopy;

/// <summary>
/// Checks the shape of a <see cref="FindCopyCommand"/>.
/// </summary>
public sealed class FindCopyCommandValidator : AbstractValidator<FindCopyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FindCopyCommandValidator"/> class.
    /// </summary>
    public FindCopyCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
