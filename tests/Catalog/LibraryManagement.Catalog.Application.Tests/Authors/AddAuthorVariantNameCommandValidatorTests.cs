using LibraryManagement.Catalog.Application.Authors.AddAuthorVariantName;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class AddAuthorVariantNameCommandValidatorTests
{
    private readonly AddAuthorVariantNameCommandValidator _validator = new();

    private static AddAuthorVariantNameCommand AnAddition(
        Guid? authorId = null,
        string variantName = "Sullivan, Vernon")
        => new(authorId ?? Guid.CreateVersion7(), variantName);

    [Fact]
    public void AWellFormedAddition_IsAccepted()
    {
        _validator.Validate(AnAddition()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(AnAddition(authorId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AddAuthorVariantNameCommand.AuthorId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ANameThatIsNotOne_IsRejected(string variantName)
    {
        var outcome = _validator.Validate(AnAddition(variantName: variantName));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AddAuthorVariantNameCommand.VariantName));
    }

    [Fact]
    public void ANameLongerThanAHeadingMayBe_IsRejected()
    {
        var outcome = _validator.Validate(AnAddition(variantName: new string('x', PersonName.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AddAuthorVariantNameCommand.VariantName));
    }

    [Fact]
    public void WhetherTheVariantDuplicatesAnother_IsNotThisValidatorsBusiness()
    {
        // Well-formed and possibly untrue: whether the author already carries this form is a
        // question about the record, and the aggregate answers it. The handler test shows the
        // command failing on the domain's answer instead.
        _validator.Validate(AnAddition(variantName: "Vian, Boris")).IsValid.ShouldBeTrue();
    }
}
