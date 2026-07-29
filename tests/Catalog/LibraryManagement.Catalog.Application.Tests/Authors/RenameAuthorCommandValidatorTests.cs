using LibraryManagement.Catalog.Application.Authors.RenameAuthor;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class RenameAuthorCommandValidatorTests
{
    private readonly RenameAuthorCommandValidator _validator = new();

    private static RenameAuthorCommand ARename(
        Guid? authorId = null,
        string newAuthorizedName = "Duchesne, Annie")
        => new(authorId ?? Guid.CreateVersion7(), newAuthorizedName);

    [Fact]
    public void AWellFormedRename_IsAccepted()
    {
        _validator.Validate(ARename()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ARename(authorId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RenameAuthorCommand.AuthorId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ANameThatIsNotOne_IsRejected(string newAuthorizedName)
    {
        var outcome = _validator.Validate(ARename(newAuthorizedName: newAuthorizedName));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RenameAuthorCommand.NewAuthorizedName));
    }

    [Fact]
    public void ANameLongerThanAHeadingMayBe_IsRejected()
    {
        var outcome = _validator.Validate(ARename(newAuthorizedName: new string('x', PersonName.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RenameAuthorCommand.NewAuthorizedName));
    }

    [Fact]
    public void EveryMistakeInARequest_IsReportedAtOnce()
    {
        var outcome = _validator.Validate(ARename(authorId: Guid.Empty, newAuthorizedName: ""));

        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(2);
    }
}
