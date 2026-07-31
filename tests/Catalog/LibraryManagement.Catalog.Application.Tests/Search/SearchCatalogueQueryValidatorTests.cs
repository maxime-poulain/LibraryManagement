using LibraryManagement.Catalog.Application.Search.SearchCatalogue;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Application.Tests.Search;

public sealed class SearchCatalogueQueryValidatorTests
{
    private readonly SearchCatalogueQueryValidator _validator = new();

    [Fact]
    public void AWellFormedSearch_IsAccepted()
    {
        _validator.Validate(new SearchCatalogueQuery("Ernaux")).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ATermThatIsNotOne_IsRejected(string searchTerm)
    {
        var outcome = _validator.Validate(new SearchCatalogueQuery(searchTerm));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(SearchCatalogueQuery.SearchTerm));
    }

    [Fact]
    public void ATermLongerThanAnyForm_IsRejected()
    {
        // Longer than the longest form the catalogue can hold, so this is not a search that
        // happens to find nothing — it is a request that never could.
        var tooLong = new string('x', Math.Max(NameForm.MaxLength, Title.MaxLength) + 1);

        var outcome = _validator.Validate(new SearchCatalogueQuery(tooLong));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(SearchCatalogueQuery.SearchTerm));
    }
}
