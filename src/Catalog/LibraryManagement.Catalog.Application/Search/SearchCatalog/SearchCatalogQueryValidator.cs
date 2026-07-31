using FluentValidation;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Application.Search.SearchCatalog;

/// <summary>
/// Checks the shape of a <see cref="SearchCatalogQuery"/>.
/// </summary>
/// <remarks>
/// The upper bound is the longest form the catalog can hold — the larger of a name form's and a
/// title's limit. A term longer than every possible form is not a search that happens to find
/// nothing; it is a request that never could, which is the difference between an empty answer and
/// a malformed question.
/// </remarks>
public sealed class SearchCatalogQueryValidator : AbstractValidator<SearchCatalogQuery>
{
    private static readonly int LongestForm = Math.Max(NameForm.MaxLength, Title.MaxLength);

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchCatalogQueryValidator"/> class.
    /// </summary>
    public SearchCatalogQueryValidator()
    {
        RuleFor(query => query.SearchTerm)
            .NotEmpty()
            .WithMessage("A search term is required.")
            .MaximumLength(LongestForm)
            .WithMessage($"A search term may not exceed {LongestForm} characters.");
    }
}
