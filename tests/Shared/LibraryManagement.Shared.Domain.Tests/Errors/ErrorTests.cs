using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Domain.Tests.Errors;

public sealed class ErrorTests
{
    private static readonly ErrorCode NotFound = new("Book.NotFound");

    [Fact]
    public void Constructor_KeepsCodeAndMessage()
    {
        var error = new Error(NotFound, "no book with that id");

        error.ErrorCode.ShouldBe(NotFound);
        error.ErrorMessage.ShouldBe("no book with that id");
    }

    [Fact]
    public void Equals_WithTheSameCodeAndMessage_IsTrueAndHashesMatch()
    {
        var left = new Error(NotFound, "same");
        var right = new Error(new ErrorCode("Book.NotFound"), "same");

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_WithADifferentMessage_IsFalse()
    {
        new Error(NotFound, "one").Equals(new Error(NotFound, "two")).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithADifferentCode_IsFalse()
    {
        new Error(NotFound, "same")
            .Equals(new Error(new ErrorCode("Loan.NotFound"), "same"))
            .ShouldBeFalse();
    }

    [Fact]
    public void ToString_PrefixesTheMessageWithTheCode()
    {
        new Error(NotFound, "no book with that id")
            .ToString()
            .ShouldBe("Book.NotFound: no book with that id");
    }
}
