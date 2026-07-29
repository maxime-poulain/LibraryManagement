using LibraryManagement.Catalog.Domain.Editions;
using static LibraryManagement.Catalog.Domain.Tests.Catalogue;

namespace LibraryManagement.Catalog.Domain.Tests.Editions;

public sealed class IsbnTests
{
    // --- What an ISBN is -------------------------------------------------------------------------

    [Fact]
    public void AThirteenDigitNumber_IsAccepted()
    {
        AnIsbn("9782070612758").Value.ShouldBe("9782070612758");
    }

    [Fact]
    public void ATenDigitNumber_IsAccepted()
    {
        // The same Petit Prince, as its title page printed it before 2007.
        AnIsbn("2070612759").Value.ShouldBe("2070612759");
    }

    [Fact]
    public void ATenDigitNumberEndingInX_IsAccepted()
    {
        // X stands for a check digit of ten; it is part of the standard, not an anomaly.
        AnIsbn("080442957X").Value.ShouldBe("080442957X");
    }

    [Fact]
    public void HyphensAndSpaces_AreGroupingAndNotIdentity()
    {
        AnIsbn("978-2-07-061275-8").ShouldBe(AnIsbn("9782070612758"));
        AnIsbn("2 07 061275 9").ShouldBe(AnIsbn("2070612759"));
    }

    [Fact]
    public void ALowercaseCheckX_IsTheSameX()
    {
        AnIsbn("080442957x").Value.ShouldBe("080442957X");
    }

    [Fact]
    public void ToString_ReturnsTheNormalizedNumber()
    {
        AnIsbn("978-2-07-061275-8").ToString().ShouldBe("9782070612758");
    }

    // --- What it refuses -------------------------------------------------------------------------

    [Fact]
    public void AMistypedDigit_IsCaughtByTheCheckDigit()
    {
        // The entire reason the check digit exists: one wrong digit at a cataloguing desk.
        var result = Isbn.Create("9782070612757");

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidIsbn);
    }

    [Fact]
    public void AMistypedTenDigitNumber_IsCaughtToo()
    {
        ErrorsOf(Isbn.Create("2070612758")).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidIsbn);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("97820706127580")]
    [InlineData("97820A0612758")]
    [InlineData("978-2-07/061275-8")]
    public void TheWrongShape_IsRefused(string value)
    {
        ErrorsOf(Isbn.Create(value)).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidIsbn);
    }

    [Fact]
    public void ThirteenDigitsOutsideTheBookPrefixes_AreNotAnIsbn()
    {
        // Checksum-valid as an article number, but 978 and 979 are what make it a *book* number.
        ErrorsOf(Isbn.Create("1234567890128")).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidIsbn);
    }

    [Fact]
    public void AnXAnywhereButLast_IsRefused()
    {
        ErrorsOf(Isbn.Create("08044295X7")).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidIsbn);
    }

    [Fact]
    public void AnXInAThirteenDigitNumber_IsRefused()
    {
        ErrorsOf(Isbn.Create("978207061275X")).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidIsbn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NothingAtAll_IsRefused(string? value)
    {
        ErrorsOf(Isbn.Create(value)).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidIsbn);
    }
}
