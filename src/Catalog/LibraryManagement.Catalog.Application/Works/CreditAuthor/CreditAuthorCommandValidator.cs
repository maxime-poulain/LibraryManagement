using FluentValidation;

namespace LibraryManagement.Catalog.Application.Works.CreditAuthor;

/// <summary>
/// Checks the shape of a <see cref="CreditAuthorCommand"/>.
/// </summary>
/// <remarks>
/// Whether the work and the author <em>exist</em> is not checked here — that is a question about
/// the catalogue rather than about the request, it needs the store to answer, and the handler asks
/// it.
/// </remarks>
public sealed class CreditAuthorCommandValidator : AbstractValidator<CreditAuthorCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreditAuthorCommandValidator"/> class.
    /// </summary>
    public CreditAuthorCommandValidator()
    {
        RuleFor(command => command.WorkId)
            .NotEmpty()
            .WithMessage("A work identifier is required.");

        RuleFor(command => command.AuthorId)
            .NotEmpty()
            .WithMessage("An author identifier is required.");
    }
}
