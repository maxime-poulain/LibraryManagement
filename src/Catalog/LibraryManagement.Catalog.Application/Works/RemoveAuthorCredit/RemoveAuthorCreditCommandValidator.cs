using FluentValidation;

namespace LibraryManagement.Catalog.Application.Works.RemoveAuthorCredit;

/// <summary>
/// Checks the shape of a <see cref="RemoveAuthorCreditCommand"/>.
/// </summary>
public sealed class RemoveAuthorCreditCommandValidator : AbstractValidator<RemoveAuthorCreditCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveAuthorCreditCommandValidator"/> class.
    /// </summary>
    public RemoveAuthorCreditCommandValidator()
    {
        RuleFor(command => command.WorkId)
            .NotEmpty()
            .WithMessage("A work identifier is required.");

        RuleFor(command => command.AuthorId)
            .NotEmpty()
            .WithMessage("An author identifier is required.");
    }
}
