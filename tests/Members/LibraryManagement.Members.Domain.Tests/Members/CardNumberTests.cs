using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Domain.Tests.Members;

public sealed class CardNumberTests
{
    [Fact]
    public void Create_KeepsTheNumberAsPrinted()
        => CardNumber.Create("20260000512").Match(number => number.Value, _ => "refused")
            .ShouldBe("20260000512");

    [Fact]
    public void Create_TrimsTheSurroundingWhiteSpace()
        => CardNumber.Create("  20260000512  ").Match(number => number.Value, _ => "refused")
            .ShouldBe("20260000512");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutANumber_Fails(string? value)
        => CardNumber.Create(value).HasErrors().ShouldBeTrue();

    [Fact]
    public void Create_ShorterThanACardMayCarry_Fails()
    {
        // A mis-scan producing one or two characters must not become a member.
        CardNumber.Create(new string('1', CardNumber.MinLength - 1)).HasErrors().ShouldBeTrue();
    }

    [Fact]
    public void Create_LongerThanACardMayRun_Fails()
        => CardNumber.Create(new string('1', CardNumber.MaxLength + 1)).HasErrors().ShouldBeTrue();

    [Fact]
    public void Create_AtTheBounds_Succeeds()
    {
        CardNumber.Create(new string('1', CardNumber.MinLength)).HasErrors().ShouldBeFalse();
        CardNumber.Create(new string('1', CardNumber.MaxLength)).HasErrors().ShouldBeFalse();
    }

    [Theory]
    [InlineData("ADH-2026-0042")]
    [InlineData("00012345")]
    [InlineData("B2026*0042")]
    public void Create_AcceptsWhateverNumberingTheSupplierUsed(string value)
    {
        // The model does not parse a card number. Cards arrive in purchased batches, and a format
        // rule would refuse a legitimate one the day a new batch does.
        CardNumber.Create(value).HasErrors().ShouldBeFalse();
    }

    [Fact]
    public void TwoNumbersDifferingOnlyInCase_AreTwoNumbers()
    {
        // A scanner reads what is printed, exactly as it does a barcode.
        CardNumber.Create("a2026").Match(number => number, _ => throw new InvalidOperationException())
            .ShouldNotBe(CardNumber.Create("A2026")
                .Match(number => number, _ => throw new InvalidOperationException()));
    }
}
