using LibraryManagement.Catalog.Application.Authors.RenameAuthor;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class RenameAuthorCommandValidatorTests
{
    private readonly RenameAuthorCommandValidator _validator = new();

    private static RenameAuthorCommand ARename(
        Guid? authorId = null,
        string newPreferredName = "Duchesne, Annie")
        => new(authorId ?? Guid.CreateVersion7(), newPreferredName);

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
    public void ANameThatIsNotOne_IsRejected(string newPreferredName)
    {
        var outcome = _validator.Validate(ARename(newPreferredName: newPreferredName));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RenameAuthorCommand.NewPreferredName));
    }

    [Fact]
    public void ANameLongerThanANameFormMayRun_IsRejected()
    {
        var outcome = _validator.Validate(ARename(newPreferredName: new string('x', NameForm.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RenameAuthorCommand.NewPreferredName));
    }

    [Fact]
    public void EveryMistakeInARequest_IsReportedAtOnce()
    {
        var outcome = _validator.Validate(ARename(authorId: Guid.Empty, newPreferredName: ""));

        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(2);
    }
}
