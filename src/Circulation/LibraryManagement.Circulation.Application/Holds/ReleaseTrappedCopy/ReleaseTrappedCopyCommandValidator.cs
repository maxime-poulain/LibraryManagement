using FluentValidation;

namespace LibraryManagement.Circulation.Application.Holds.ReleaseTrappedCopy;

/// <summary>
/// Checks the shape of a <see cref="ReleaseTrappedCopyCommand"/>.
/// </summary>
public sealed class ReleaseTrappedCopyCommandValidator : AbstractValidator<ReleaseTrappedCopyCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseTrappedCopyCommandValidator"/> class.
    /// </summary>
    public ReleaseTrappedCopyCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
