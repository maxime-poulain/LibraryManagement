using FluentValidation;

namespace LibraryManagement.Circulation.Application.Borrowers.GetBorrowerFile;

/// <summary>
/// Checks the shape of a <see cref="GetBorrowerFileQuery"/>.
/// </summary>
/// <remarks>
/// An empty identifier is not a question. Nothing else is checkable here: this context keeps no
/// register of people, so there is no such thing as an unknown borrower to refuse — only a
/// borrower with nothing on file, which the handler answers.
/// </remarks>
public sealed class GetBorrowerFileQueryValidator : AbstractValidator<GetBorrowerFileQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetBorrowerFileQueryValidator"/> class.
    /// </summary>
    public GetBorrowerFileQueryValidator()
    {
        RuleFor(query => query.BorrowerId)
            .NotEmpty()
            .WithMessage("A borrower identifier is required.");
    }
}
