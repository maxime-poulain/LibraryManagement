using LibraryManagement.Circulation.Application.Holds.CombineHoldsOfMergedBorrower;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The shape of a member merge as it reaches this module's queues: two identifiers, and they are
/// two.
/// </summary>
public sealed class CombineHoldsOfMergedBorrowerCommandValidatorTests
{
    private static readonly CombineHoldsOfMergedBorrowerCommandValidator Validator = new();

    [Fact]
    public void AWellFormedCommand_IsAccepted()
    {
        var outcome = Validator.Validate(
            new CombineHoldsOfMergedBorrowerCommand(Guid.CreateVersion7(), Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false, "AbsorbedBorrowerId")]
    [InlineData(false, true, "SurvivingBorrowerId")]
    public void AnEmptyIdentifier_IsRejected(bool absorbedEmpty, bool survivingEmpty, string property)
    {
        var outcome = Validator.Validate(new CombineHoldsOfMergedBorrowerCommand(
            absorbedEmpty ? Guid.Empty : Guid.CreateVersion7(),
            survivingEmpty ? Guid.Empty : Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == property);
    }

    [Fact]
    public void OneRecordNamedTwice_IsRejected()
    {
        var member = Guid.CreateVersion7();

        var outcome = Validator.Validate(new CombineHoldsOfMergedBorrowerCommand(member, member));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == "SurvivingBorrowerId");
    }
}
