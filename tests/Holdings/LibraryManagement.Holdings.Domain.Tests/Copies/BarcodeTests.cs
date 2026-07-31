using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Domain.Tests.Copies;

public sealed class BarcodeTests
{
    [Fact]
    public void Create_KeepsTheLabelAsPrinted()
        => Barcode.Create("30124000512").Match(barcode => barcode.Value, _ => "refused")
            .ShouldBe("30124000512");

    [Fact]
    public void Create_TrimsTheSurroundingWhiteSpace()
        => Barcode.Create("  30124000512  ").Match(barcode => barcode.Value, _ => "refused")
            .ShouldBe("30124000512");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutALabel_Fails(string? value)
        => Barcode.Create(value).HasErrors().ShouldBeTrue();

    [Fact]
    public void Create_ShorterThanALabelMayBe_Fails()
    {
        // A mis-scan producing one or two characters must not become a copy.
        Barcode.Create(new string('1', Barcode.MinLength - 1)).HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void Create_LongerThanALabelMayRun_Fails()
        => Barcode.Create(new string('1', Barcode.MaxLength + 1)).HasErrors().ShouldBeTrue();

    [Fact]
    public void Create_AtTheBounds_Succeeds()
    {
        Barcode.Create(new string('1', Barcode.MinLength)).HasErrors().ShouldBeFalse();
        Barcode.Create(new string('1', Barcode.MaxLength)).HasErrors().ShouldBeFalse();
    }

    [Theory]
    [InlineData("CODABAR-A1234B")]
    [InlineData("9782070612758")]
    [InlineData("*30124000512*")]
    public void Create_AcceptsWhateverSymbologyTheScannerWasSoldWith(string value)
    {
        // The model does not parse a barcode. A library prints them in pre-bought ranges, and a
        // format rule would refuse a legitimate label the day a new batch arrives.
        Barcode.Create(value).HasErrors().ShouldBeFalse();
    }

    [Fact]
    public void TwoLabelsDifferingOnlyInCase_AreTwoLabels()
    {
        // A scanner reads what is printed, and a library that printed both has issued two.
        Barcode.Create("a1234").Match(b => b, _ => throw new InvalidOperationException())
            .ShouldNotBe(Barcode.Create("A1234").Match(b => b, _ => throw new InvalidOperationException()));
    }
}
