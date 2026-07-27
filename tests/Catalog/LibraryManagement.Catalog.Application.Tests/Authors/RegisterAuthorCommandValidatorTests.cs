using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class RegisterAuthorCommandValidatorTests
{
    private readonly RegisterAuthorCommandValidator _validator = new();

    private static RegisterAuthorCommand ARegistration(
        Guid? authorId = null,
        string authorizedName = "Ernaux, Annie",
        int? birthYear = 1940,
        int? deathYear = null)
        => new(authorId ?? Guid.CreateVersion7(), authorizedName, birthYear, deathYear);

    private static string TooLongAName() => new('x', PersonName.MaxLength + 1);

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedRegistration_IsAccepted()
    {
        _validator.Validate(ARegistration()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ARegistrationWithNeitherYear_IsAccepted()
    {
        // A catalogue frequently knows a name and nothing else. Demanding a year would make the
        // ordinary case the exception.
        _validator.Validate(ARegistration(birthYear: null, deathYear: null)).IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        // The one rule this validator exists for above the others. AuthorId refuses the empty Guid by
        // throwing, because an identifier that is not one is a programmer error — but a request from
        // outside can carry it, and that is a missing field. Take this rule away and the handler's
        // conversion throws on a request an employee could have been told to correct.
        var outcome = _validator.Validate(ARegistration(authorId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterAuthorCommand.AuthorId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ANameThatIsNotOne_IsRejected(string authorizedName)
    {
        var outcome = _validator.Validate(ARegistration(authorizedName: authorizedName));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterAuthorCommand.AuthorizedName));
    }

    [Fact]
    public void ANameLongerThanAHeadingMayBe_IsRejected()
    {
        var outcome = _validator.Validate(ARegistration(authorizedName: TooLongAName()));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterAuthorCommand.AuthorizedName));
    }

    [Theory]
    [InlineData(LifeYears.EarliestYear - 1)]
    [InlineData(LifeYears.LatestYear + 1)]
    public void AYearOfBirthOutsideWhatACatalogueAccepts_IsRejected(int birthYear)
    {
        var outcome = _validator.Validate(ARegistration(birthYear: birthYear));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterAuthorCommand.BirthYear));
    }

    [Theory]
    [InlineData(LifeYears.EarliestYear - 1)]
    [InlineData(LifeYears.LatestYear + 1)]
    public void AYearOfDeathOutsideWhatACatalogueAccepts_IsRejected(int deathYear)
    {
        var outcome = _validator.Validate(ARegistration(deathYear: deathYear));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterAuthorCommand.DeathYear));
    }

    [Fact]
    public void EveryMistakeInARequest_IsReportedAtOnce()
    {
        // Telling an employee about the second mistake only after they fixed the first is a worse
        // experience than one round trip. The accumulation is the point of a validator running before
        // the handler rather than a guard clause inside it.
        var outcome = _validator.Validate(
            ARegistration(authorId: Guid.Empty, authorizedName: "", birthYear: LifeYears.LatestYear + 1));

        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(3);
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void AYearOfDeathBeforeTheYearOfBirth_IsNotThisValidatorsBusiness()
    {
        // Well-formed and untrue: both years are years, and no field is missing. Whether one person
        // can have lived them is a rule about a person, and it belongs to LifeYears. Restating it
        // here would create a second source of truth that drifts from the first — the pipeline test
        // shows the command failing on the domain's answer instead.
        _validator.Validate(ARegistration(birthYear: 1944, deathYear: 1900)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether an author is already catalogued under that name is a question about the catalogue,
        // needs the store to answer, and is not asked here. A validator with a dependency is a
        // validator that has started making decisions the handler owns.
        typeof(RegisterAuthorCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
