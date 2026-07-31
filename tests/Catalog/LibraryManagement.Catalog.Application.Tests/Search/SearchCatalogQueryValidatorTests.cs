using LibraryManagement.Catalog.Application.Search.SearchCatalog;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Application.Tests.Search;

public sealed class SearchCatalogQueryValidatorTests
{
    private readonly SearchCatalogQueryValidator _validator = new();

    [Fact]
    public void AWellFormedSearch_IsAccepted()
    {
        _validator.Validate(new SearchCatalogQuery("Ernaux")).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ATermThatIsNotOne_IsRejected(string formPrefix)
    {
        var outcome = _validator.Validate(new SearchCatalogQuery(formPrefix));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(SearchCatalogQuery.FormPrefix));
    }

    [Fact]
    public void ATermLongerThanAnyForm_IsRejected()
    {
        // Longer than the longest form the catalog can hold, so this is not a search that
        // happens to find nothing — it is a request that never could.
        var tooLong = new string('x', Math.Max(NameForm.MaxLength, Title.MaxLength) + 1);

        var outcome = _validator.Validate(new SearchCatalogQuery(tooLong));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(SearchCatalogQuery.FormPrefix));
    }
}
