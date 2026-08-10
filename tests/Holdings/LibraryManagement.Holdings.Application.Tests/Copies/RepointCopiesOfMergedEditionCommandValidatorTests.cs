using LibraryManagement.Holdings.Application.Copies.RepointCopiesOfMergedEdition;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

/// <summary>
/// The shape of a merge as it reaches this module: two identifiers, and they are two.
/// </summary>
public sealed class RepointCopiesOfMergedEditionCommandValidatorTests
{
    private static readonly RepointCopiesOfMergedEditionCommandValidator Validator = new();

    [Fact]
    public void AWellFormedCommand_IsAccepted()
    {
        var outcome = Validator.Validate(
            new RepointCopiesOfMergedEditionCommand(Guid.CreateVersion7(), Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false, "AbsorbedEditionId")]
    [InlineData(false, true, "SurvivingEditionId")]
    public void AnEmptyIdentifier_IsRejected(bool absorbedEmpty, bool survivingEmpty, string property)
    {
        var outcome = Validator.Validate(new RepointCopiesOfMergedEditionCommand(
            absorbedEmpty ? Guid.Empty : Guid.CreateVersion7(),
            survivingEmpty ? Guid.Empty : Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == property);
    }

    [Fact]
    public void OneRecordNamedTwice_IsRejected()
    {
        // Catalog refuses a record merged into itself long before anything is announced, so this
        // never arrives from the drain. It is checked here because a command answers for its own
        // shape, and this one would otherwise sweep an edition's whole shelf to repoint it where it
        // already points.
        var edition = Guid.CreateVersion7();

        var outcome = Validator.Validate(
            new RepointCopiesOfMergedEditionCommand(edition, edition));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == "SurvivingEditionId");
    }
}
