using LibraryManagement.Holdings.Application.Copies.RelabelCopy;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

public sealed class RelabelCopyCommandValidatorTests
{
    private readonly RelabelCopyCommandValidator _validator = new();

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedRelabelling_IsAccepted()
    {
        _validator.Validate(new RelabelCopyCommand(Guid.CreateVersion7(), "30124000999"))
            .IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(new RelabelCopyCommand(Guid.Empty, "30124000999"));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RelabelCopyCommand.CopyId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    public void ABarcodeShorterThanALabelMayCarry_IsRejected(string barcode)
    {
        var outcome = _validator.Validate(new RelabelCopyCommand(Guid.CreateVersion7(), barcode));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RelabelCopyCommand.Barcode));
    }

    [Fact]
    public void ABarcodeLongerThanALabelMayRun_IsRejected()
    {
        var outcome = _validator.Validate(
            new RelabelCopyCommand(Guid.CreateVersion7(), new string('1', Barcode.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RelabelCopyCommand.Barcode));
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether the label is already on another copy needs the store, and the handler asks —
        // which is how the refusal gets to name the label instead of saying "invalid".
        typeof(RelabelCopyCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
