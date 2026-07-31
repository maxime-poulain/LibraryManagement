using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using static LibraryManagement.Catalog.Domain.Tests.Catalogue;

namespace LibraryManagement.Catalog.Domain.Tests.Authors;

public sealed class NameFormTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutAName_Fails(string? value)
    {
        ErrorsOf(NameForm.Create(value))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidName);
    }

    [Fact]
    public void Create_TrimsTheSurroundingWhiteSpace()
    {
        Name("  Ernaux, Annie  ").Value.ShouldBe("Ernaux, Annie");
    }

    [Fact]
    public void Create_LongerThanAHeadingMayRun_Fails()
    {
        ErrorsOf(NameForm.Create(new string('x', NameForm.MaxLength + 1)))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidName);
    }

    [Fact]
    public void Create_AtExactlyTheLimit_Succeeds()
    {
        Name(new string('x', NameForm.MaxLength)).Value.Length.ShouldBe(NameForm.MaxLength);
    }

    [Fact]
    public void Equality_IsCaseSensitive()
    {
        // "de Beauvoir" and "De Beauvoir" are different filing decisions a librarian makes on
        // purpose, so the catalogue must not silently merge them.
        Name("de Beauvoir, Simone").ShouldNotBe(Name("De Beauvoir, Simone"));
    }
}

public sealed class TitleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithoutATitle_Fails(string? value)
    {
        ErrorsOf(Title.Create(value)).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidTitle);
    }

    [Fact]
    public void Create_LongerThanATitleMayRun_Fails()
    {
        ErrorsOf(Title.Create(new string('x', Title.MaxLength + 1)))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidTitle);
    }

    [Fact]
    public void Equality_IsStructural()
    {
        TitleOf("Le Petit Prince").ShouldBe(TitleOf("Le Petit Prince"));
    }
}

public sealed class LifeYearsTests
{
    [Fact]
    public void Unknown_HoldsNeitherYear()
    {
        LifeYears.Unknown.Birth.ShouldBeNull();
        LifeYears.Unknown.Death.ShouldBeNull();
    }

    [Fact]
    public void Create_WithADeathBeforeTheBirth_Fails()
    {
        ErrorsOf(LifeYears.Create(1944, 1900))
            .Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidLifeYears);
    }

    [Fact]
    public void Create_WithOnlyOneYearKnown_Succeeds()
    {
        Years(1940, null).Birth.ShouldBe(1940);
        Years(null, 1944).Death.ShouldBe(1944);
    }

    [Fact]
    public void Create_ReportsEveryProblemAtOnce()
    {
        // Both years out of range and inverted: an employee should see all of it, not one per try.
        var errors = ErrorsOf(LifeYears.Create(0, LifeYears.LatestYear + 1));

        errors.Count.ShouldBe(2);
    }

    [Fact]
    public void ToString_PrintsTheFormACatalogueUses()
    {
        Years(1900, 1944).ToString().ShouldBe("1900-1944");
        Years(1940, null).ToString().ShouldBe("1940-");
        LifeYears.Unknown.ToString().ShouldBe(string.Empty);
    }
}
