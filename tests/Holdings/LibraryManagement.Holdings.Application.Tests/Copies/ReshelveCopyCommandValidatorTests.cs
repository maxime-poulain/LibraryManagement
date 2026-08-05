using LibraryManagement.Holdings.Application.Copies.ReshelveCopy;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

public sealed class ReshelveCopyCommandValidatorTests
{
    private readonly ReshelveCopyCommandValidator _validator = new();

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedMove_IsAccepted()
    {
        _validator.Validate(new ReshelveCopyCommand(Guid.CreateVersion7(), "JEUN 843.912 SAI"))
            .IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(new ReshelveCopyCommand(Guid.Empty, "843.912 SAI"));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ReshelveCopyCommand.CopyId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AShelfmarkThatIsNotOne_IsRejected(string shelfmark)
    {
        var outcome = _validator.Validate(new ReshelveCopyCommand(Guid.CreateVersion7(), shelfmark));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ReshelveCopyCommand.Shelfmark));
    }

    [Fact]
    public void AShelfmarkPastItsBound_IsRejected()
    {
        var outcome = _validator.Validate(new ReshelveCopyCommand(
            Guid.CreateVersion7(), new string('x', Shelfmark.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ReshelveCopyCommand.Shelfmark));
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // A shelfmark is a decision already taken, not a value to verify against Catalog: the
        // labels on the shelves do not move when a preferred name is corrected, and neither does
        // this.
        typeof(ReshelveCopyCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
