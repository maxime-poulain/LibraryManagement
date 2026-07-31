using FluentValidation;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Application.Works.RegisterWork;

/// <summary>
/// Checks the shape of a <see cref="RegisterWorkCommand"/>.
/// </summary>
/// <remarks>
/// Whether the authors <em>exist</em> is not checked here. That is a question about the catalog
/// rather than about the request, it needs the store to answer, and the handler asks it.
/// </remarks>
public sealed class RegisterWorkCommandValidator : AbstractValidator<RegisterWorkCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterWorkCommandValidator"/> class.
    /// </summary>
    public RegisterWorkCommandValidator()
    {
        RuleFor(command => command.WorkId)
            .NotEmpty()
            .WithMessage("A work identifier is required.");

        RuleFor(command => command.PreferredTitle)
            .NotEmpty()
            .WithMessage("A title is required.")
            .MaximumLength(Title.MaxLength)
            .WithMessage($"A title may not exceed {Title.MaxLength} characters.");

        RuleFor(command => command.AuthorIds)
            .NotNull()
            .WithMessage("A list of authors is required, even an empty one.");

        RuleForEach(command => command.AuthorIds)
            .NotEmpty()
            .WithMessage("An author identifier is required.");

        RuleFor(command => command.AuthorIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .When(command => command.AuthorIds is not null)
            .WithMessage("The same author is credited more than once.");
    }
}
