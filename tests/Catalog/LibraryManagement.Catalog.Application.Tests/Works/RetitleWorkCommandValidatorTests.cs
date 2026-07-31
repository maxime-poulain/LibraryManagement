using LibraryManagement.Catalog.Application.Works.RetitleWork;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class RetitleWorkCommandValidatorTests
{
    private readonly RetitleWorkCommandValidator _validator = new();

    private static RetitleWorkCommand ARetitle(
        Guid? workId = null,
        string title = "La Horde du Contrevent")
        => new(workId ?? Guid.CreateVersion7(), title);

    [Fact]
    public void AWellFormedRetitle_IsAccepted()
    {
        _validator.Validate(ARetitle()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ARetitle(workId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RetitleWorkCommand.WorkId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ATitleThatIsNotOne_IsRejected(string title)
    {
        var outcome = _validator.Validate(ARetitle(title: title));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RetitleWorkCommand.NewPreferredTitle));
    }

    [Fact]
    public void ATitleLongerThanATitleMayBe_IsRejected()
    {
        var outcome = _validator.Validate(ARetitle(title: new string('x', Title.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RetitleWorkCommand.NewPreferredTitle));
    }
}
