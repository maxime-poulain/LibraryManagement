using LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedEdition;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

/// <summary>
/// The shape of a merge as it reaches this module: two identifiers, and they are two. Its own file
/// rather than a case in <see cref="CommandValidatorTests"/>, which covers the commands whose whole
/// shape is identifiers standing alone — this one has a rule about the pair.
/// </summary>
public sealed class RepointLoansOfMergedEditionCommandValidatorTests
{
    private static readonly RepointLoansOfMergedEditionCommandValidator Validator = new();

    [Fact]
    public void AWellFormedCommand_IsAccepted()
    {
        var outcome = Validator.Validate(
            new RepointLoansOfMergedEditionCommand(Guid.CreateVersion7(), Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false, "AbsorbedEditionId")]
    [InlineData(false, true, "SurvivingEditionId")]
    public void AnEmptyIdentifier_IsRejected(bool absorbedEmpty, bool survivingEmpty, string property)
    {
        var outcome = Validator.Validate(new RepointLoansOfMergedEditionCommand(
            absorbedEmpty ? Guid.Empty : Guid.CreateVersion7(),
            survivingEmpty ? Guid.Empty : Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == property);
    }

    [Fact]
    public void OneRecordNamedTwice_IsRejected()
    {
        // Catalog refuses a record merged into itself long before anything is announced, so this
        // never arrives from the drain. It is checked because a command answers for its own shape,
        // and this one would otherwise sweep every loan of an edition to repoint them where they
        // already point.
        var edition = Guid.CreateVersion7();

        var outcome = Validator.Validate(
            new RepointLoansOfMergedEditionCommand(edition, edition));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == "SurvivingEditionId");
    }
}
