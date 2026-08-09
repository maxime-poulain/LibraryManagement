using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.NoteCopyAccountedFor;

/// <summary>
/// Checks the shape of a <see cref="NoteCopyAccountedForCommand"/>.
/// </summary>
public sealed class NoteCopyAccountedForCommandValidator
    : AbstractValidator<NoteCopyAccountedForCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoteCopyAccountedForCommandValidator"/> class.
    /// </summary>
    public NoteCopyAccountedForCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
