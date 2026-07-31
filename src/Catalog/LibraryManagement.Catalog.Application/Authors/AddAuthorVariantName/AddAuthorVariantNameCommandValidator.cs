using FluentValidation;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Authors.AddAuthorVariantName;

/// <summary>
/// Checks the shape of an <see cref="AddAuthorVariantNameCommand"/>.
/// </summary>
public sealed class AddAuthorVariantNameCommandValidator : AbstractValidator<AddAuthorVariantNameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddAuthorVariantNameCommandValidator"/> class.
    /// </summary>
    public AddAuthorVariantNameCommandValidator()
    {
        RuleFor(command => command.AuthorId)
            .NotEmpty()
            .WithMessage("An author identifier is required.");

        RuleFor(command => command.VariantName)
            .NotEmpty()
            .WithMessage("A variant name is required.")
            .MaximumLength(NameForm.MaxLength)
            .WithMessage($"A variant name may not exceed {NameForm.MaxLength} characters.");
    }
}
