using FluentValidation;

namespace LibraryManagement.Catalog.Application.Works.GetWorkById;

/// <summary>
/// Checks the shape of a <see cref="GetWorkByIdQuery"/>.
/// </summary>
/// <remarks>
/// Whether a work exists is not checked here, and could not be: that is a question about the
/// catalog, it needs the store to answer, and the handler asks it. What is checked is that the
/// caller asked a question at all — an empty identifier is not one, and it is the difference between
/// a malformed request and a work that is genuinely not cataloged.
/// </remarks>
public sealed class GetWorkByIdQueryValidator : AbstractValidator<GetWorkByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetWorkByIdQueryValidator"/> class.
    /// </summary>
    public GetWorkByIdQueryValidator()
    {
        RuleFor(query => query.WorkId)
            .NotEmpty()
            .WithMessage("A work identifier is required.");
    }
}
