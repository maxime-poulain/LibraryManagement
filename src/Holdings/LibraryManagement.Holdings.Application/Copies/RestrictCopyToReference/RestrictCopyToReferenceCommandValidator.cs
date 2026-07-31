using FluentValidation;

namespace LibraryManagement.Holdings.Application.Copies.RestrictCopyToReference;

/// <summary>
/// Checks the shape of a <see cref="RestrictCopyToReferenceCommand"/>.
/// </summary>
public sealed class RestrictCopyToReferenceCommandValidator : AbstractValidator<RestrictCopyToReferenceCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RestrictCopyToReferenceCommandValidator"/> class.
    /// </summary>
    public RestrictCopyToReferenceCommandValidator()
    {
        RuleFor(command => command.CopyId)
            .NotEmpty()
            .WithMessage("A copy identifier is required.");
    }
}
