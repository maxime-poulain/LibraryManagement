using FluentValidation;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Authors.RenameAuthor;

/// <summary>
/// Checks the shape of a <see cref="RenameAuthorCommand"/>.
/// </summary>
public sealed class RenameAuthorCommandValidator : AbstractValidator<RenameAuthorCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenameAuthorCommandValidator"/> class.
    /// </summary>
    public RenameAuthorCommandValidator()
    {
        RuleFor(command => command.AuthorId)
            .NotEmpty()
            .WithMessage("An author identifier is required.");

        RuleFor(command => command.NewAuthorizedName)
            .NotEmpty()
            .WithMessage("An authorized name is required.")
            .MaximumLength(PersonName.MaxLength)
            .WithMessage($"An authorized name may not exceed {PersonName.MaxLength} characters.");
    }
}
