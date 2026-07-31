using LibraryManagement.Catalog.Application.Authors.CorrectAuthorPreferredName;
using LibraryManagement.Catalog.Domain.Authors;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class CorrectAuthorPreferredNameCommandValidatorTests
{
    private readonly CorrectAuthorPreferredNameCommandValidator _validator = new();

    private static CorrectAuthorPreferredNameCommand ACorrection(
        Guid? authorId = null,
        string correctedName = "Hugo, Victor")
        => new(authorId ?? Guid.CreateVersion7(), correctedName);

    [Fact]
    public void AWellFormedCorrection_IsAccepted()
    {
        _validator.Validate(ACorrection()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ACorrection(authorId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(CorrectAuthorPreferredNameCommand.AuthorId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ANameThatIsNotOne_IsRejected(string correctedName)
    {
        var outcome = _validator.Validate(ACorrection(correctedName: correctedName));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(CorrectAuthorPreferredNameCommand.CorrectedName));
    }

    [Fact]
    public void ANameLongerThanANameFormMayRun_IsRejected()
    {
        var outcome = _validator.Validate(ACorrection(correctedName: new string('x', NameForm.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(CorrectAuthorPreferredNameCommand.CorrectedName));
    }
}
