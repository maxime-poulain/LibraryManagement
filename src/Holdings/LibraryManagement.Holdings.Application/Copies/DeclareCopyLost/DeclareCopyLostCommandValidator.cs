using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.DeclareCopyLost;

/// <summary>
/// Checks the shape of a <see cref="DeclareCopyLostCommand"/>.
/// </summary>
public sealed class DeclareCopyLostCommandValidator : AbstractValidator<DeclareCopyLostCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeclareCopyLostCommandValidator"/> class.
    /// </summary>
    public DeclareCopyLostCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
