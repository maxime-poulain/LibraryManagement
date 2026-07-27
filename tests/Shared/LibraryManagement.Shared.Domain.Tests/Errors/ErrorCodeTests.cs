using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Shared.Domain.Tests.Errors;

public sealed class ErrorCodeTests
{
    [Fact]
    public void Constructor_KeepsTheSuppliedValue()
    {
        new ErrorCode("Book.NotFound").Value.ShouldBe("Book.NotFound");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Constructor_WithABlankValue_Throws(string value)
    {
        Should.Throw<ArgumentException>(() => new ErrorCode(value));
    }

    [Fact]
    public void Constructor_WithNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new ErrorCode(null!));
    }

    [Fact]
    public void Equals_WithTheSameValue_IsTrueAndHashesMatch()
    {
        var left = new ErrorCode("Book.NotFound");
        var right = new ErrorCode("Book.NotFound");

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_AcrossContextsUsingTheSameReason_IsFalse()
    {
        // The context prefix is what keeps two modules from claiming the same code.
        new ErrorCode("Book.NotFound").Equals(new ErrorCode("Loan.NotFound")).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_ComparesByValue()
    {
        (new ErrorCode("A") == new ErrorCode("A")).ShouldBeTrue();
        (new ErrorCode("A") != new ErrorCode("B")).ShouldBeTrue();
    }

    [Fact]
    public void ToString_ReturnsTheValue()
    {
        new ErrorCode("Book.NotFound").ToString().ShouldBe("Book.NotFound");
    }

    [Fact]
    public void UsedAsADictionaryKey_IsFoundByAnEquivalentInstance()
    {
        var statuses = new Dictionary<ErrorCode, int> { [new ErrorCode("Book.NotFound")] = 404 };

        statuses[new ErrorCode("Book.NotFound")].ShouldBe(404);
    }
}
