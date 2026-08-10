using LibraryManagement.Members.Application.Members.MergeMembers;

namespace LibraryManagement.Members.Application.Tests.Members;

/// <summary>
/// The shape of a merge as it arrives from the desk: two identifiers.
/// </summary>
/// <remarks>
/// It does not check that the two differ, unlike the commands a subscriber dispatches. Those are
/// built from a contract and could only name one record twice through a defect; this one is typed
/// by a person, and telling them *a record cannot be merged into itself* is the aggregate's job —
/// where the refusal reads as a fact about the records rather than as a malformed request.
/// </remarks>
public sealed class MergeMembersCommandValidatorTests
{
    private static readonly MergeMembersCommandValidator Validator = new();

    [Fact]
    public void AWellFormedCommand_IsAccepted()
    {
        var outcome = Validator.Validate(
            new MergeMembersCommand(Guid.CreateVersion7(), Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false, "AbsorbedMemberId")]
    [InlineData(false, true, "SurvivingMemberId")]
    public void AnEmptyIdentifier_IsRejected(bool absorbedEmpty, bool survivingEmpty, string property)
    {
        var outcome = Validator.Validate(new MergeMembersCommand(
            absorbedEmpty ? Guid.Empty : Guid.CreateVersion7(),
            survivingEmpty ? Guid.Empty : Guid.CreateVersion7()));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == property);
    }

    [Fact]
    public void OneRecordNamedTwice_IsLeftToTheAggregate()
    {
        var member = Guid.CreateVersion7();

        Validator.Validate(new MergeMembersCommand(member, member)).IsValid.ShouldBeTrue();
    }
}
