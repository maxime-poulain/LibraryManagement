using FluentValidation;
using LibraryManagement.Catalog.Domain.Editions;

namespace LibraryManagement.Catalog.Application.Editions.RegisterEdition;

/// <summary>
/// Checks the shape of a <see cref="RegisterEditionCommand"/>.
/// </summary>
/// <remarks>
/// An absent ISBN is <see langword="null"/>, never blank: a blank one is a malformed request, not a
/// statement that the edition bears none. Whether the digits actually form an ISBN — the prefix,
/// the check digit — is the value object's rule, and the pipeline shows the command failing on the
/// domain's answer rather than restating it here.
/// </remarks>
public sealed class RegisterEditionCommandValidator : AbstractValidator<RegisterEditionCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterEditionCommandValidator"/> class.
    /// </summary>
    public RegisterEditionCommandValidator()
    {
        RuleFor(command => command.EditionId)
            .NotEmpty()
            .WithMessage("An edition identifier is required.");

        RuleFor(command => command.WorkId)
            .NotEmpty()
            .WithMessage("A work identifier is required.");

        RuleFor(command => command.Isbn)
            .NotEmpty()
            .WithMessage("An ISBN, when given, may not be blank.")
            .MaximumLength(Isbn.LongestWrittenForm)
            .WithMessage($"An ISBN may not exceed {Isbn.LongestWrittenForm} characters as written.")
            .When(command => command.Isbn is not null);
    }
}
