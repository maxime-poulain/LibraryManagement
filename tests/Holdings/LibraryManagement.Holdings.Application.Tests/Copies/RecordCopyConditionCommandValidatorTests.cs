using LibraryManagement.Holdings.Application.Copies.RecordCopyCondition;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

public sealed class RecordCopyConditionCommandValidatorTests
{
    private readonly RecordCopyConditionCommandValidator _validator = new();

    // --- What a well-formed request looks like ---------------------------------------------------

    [Theory]
    [InlineData(CopyCondition.Good)]
    [InlineData(CopyCondition.Worn)]
    [InlineData(CopyCondition.Damaged)]
    public void EveryConditionInTheSet_IsAccepted(CopyCondition condition)
    {
        _validator.Validate(new RecordCopyConditionCommand(Guid.CreateVersion7(), condition))
            .IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(
            new RecordCopyConditionCommand(Guid.Empty, CopyCondition.Worn));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RecordCopyConditionCommand.CopyId));
    }

    [Fact]
    public void AConditionOutsideTheSet_IsRejected()
    {
        // An enum arriving from outside carries whatever integer the caller sent, so a value the
        // type does not define reaches here perfectly typed.
        var outcome = _validator.Validate(
            new RecordCopyConditionCommand(Guid.CreateVersion7(), (CopyCondition)99));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RecordCopyConditionCommand.Condition));
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Condition decides nothing today — recording it on a withdrawn copy is the aggregate's
        // refusal, judged against state the shape cannot see.
        typeof(RecordCopyConditionCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
