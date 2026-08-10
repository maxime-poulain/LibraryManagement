using LibraryManagement.Circulation.Application.Holds.MergeHoldQueues;

namespace LibraryManagement.Circulation.Application.Tests.Holds;

/// <summary>
/// The shape of a merge as it reaches this module's queues: two identifiers, and they are two.
/// </summary>
public sealed class MergeHoldQueuesCommandValidatorTests
{
    private static readonly MergeHoldQueuesCommandValidator Validator = new();

    [Fact]
    public void AWellFormedCommand_IsAccepted()
    {
        var outcome = Validator.Validate(
            new MergeHoldQueuesCommand(Guid.CreateVersion7(), Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false, "AbsorbedEditionId")]
    [InlineData(false, true, "SurvivingEditionId")]
    public void AnEmptyIdentifier_IsRejected(bool absorbedEmpty, bool survivingEmpty, string property)
    {
        var outcome = Validator.Validate(new MergeHoldQueuesCommand(
            absorbedEmpty ? Guid.Empty : Guid.CreateVersion7(),
            survivingEmpty ? Guid.Empty : Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == property);
    }

    [Fact]
    public void OneRecordNamedTwice_IsRejected()
    {
        var edition = Guid.CreateVersion7();

        var outcome = Validator.Validate(new MergeHoldQueuesCommand(edition, edition));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == "SurvivingEditionId");
    }
}
