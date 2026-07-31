using LibraryManagement.Catalog.Application.Authors.CorrectAuthorLifeYears;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class CorrectAuthorLifeYearsCommandValidatorTests
{
    private readonly CorrectAuthorLifeYearsCommandValidator _validator = new();

    private static CorrectAuthorLifeYearsCommand ACorrection(
        Guid? authorId = null,
        int? birthYear = 1940,
        int? deathYear = null)
        => new(authorId ?? Guid.CreateVersion7(), birthYear, deathYear);

    [Fact]
    public void AWellFormedCorrection_IsAccepted()
    {
        _validator.Validate(ACorrection()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ACorrectionToUnknownYears_IsAccepted()
    {
        // Both years absent is a correction of its own — the years are not known after all — and
        // not a request with missing fields.
        _validator.Validate(ACorrection(birthYear: null, deathYear: null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ACorrection(authorId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(CorrectAuthorLifeYearsCommand.AuthorId));
    }

    [Theory]
    [InlineData(LifeYears.EarliestYear - 1)]
    [InlineData(LifeYears.LatestYear + 1)]
    public void AYearOfBirthOutsideWhatACatalogAccepts_IsRejected(int birthYear)
    {
        var outcome = _validator.Validate(ACorrection(birthYear: birthYear));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(CorrectAuthorLifeYearsCommand.BirthYear));
    }

    [Theory]
    [InlineData(LifeYears.EarliestYear - 1)]
    [InlineData(LifeYears.LatestYear + 1)]
    public void AYearOfDeathOutsideWhatACatalogAccepts_IsRejected(int deathYear)
    {
        var outcome = _validator.Validate(ACorrection(deathYear: deathYear));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(CorrectAuthorLifeYearsCommand.DeathYear));
    }

    [Fact]
    public void AYearOfDeathBeforeTheYearOfBirth_IsNotThisValidatorsBusiness()
    {
        // The same division as at registration: both years are years, and whether one person can
        // have lived them belongs to LifeYears. The handler test shows the command failing on the
        // domain's answer.
        _validator.Validate(ACorrection(birthYear: 1944, deathYear: 1900)).IsValid.ShouldBeTrue();
    }
}
