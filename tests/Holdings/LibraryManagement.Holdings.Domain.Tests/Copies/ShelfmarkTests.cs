using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Domain.Tests.Copies;

public sealed class ShelfmarkTests
{
    [Theory]
    [InlineData("843.912 SAI")]
    [InlineData("JEUN 843.912 SAI")]
    [InlineData("BD DEL")]
    public void Create_AcceptsWhatALibraryActuallyPrints(string value)
        => Shelfmark.Create(value).HasErrors().ShouldBeFalse();

    [Fact]
    public void Create_TrimsTheSurroundingWhiteSpace()
        => Shelfmark.Create("  843.912 SAI ").Match(mark => mark.Value, _ => "refused")
            .ShouldBe("843.912 SAI");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithoutAShelfmark_Fails(string? value)
        => Shelfmark.Create(value).HasErrors().ShouldBeTrue();

    [Fact]
    public void Create_LongerThanAShelfmarkMayRun_Fails()
        => Shelfmark.Create(new string('x', Shelfmark.MaxLength + 1)).HasErrors().ShouldBeTrue();
}
