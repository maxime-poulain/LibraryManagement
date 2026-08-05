using LibraryManagement.Members.Application.Members;
using LibraryManagement.Members.Application.Members.ChangeMemberGuardian;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class ChangeMemberGuardianCommandValidatorTests
{
    private readonly ChangeMemberGuardianCommandValidator _validator = new();

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void RecordingAGuardian_IsAccepted()
    {
        _validator.Validate(new ChangeMemberGuardianCommand(
                Guid.CreateVersion7(), new GuardianDetails("Gilberte", "Doinel")))
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void RemovingTheGuardian_IsAWellFormedCommand()
    {
        // Null is the removal, not a forgotten field: a protected adult whose measure ends is
        // reached directly again, and the command must be able to say so.
        _validator.Validate(new ChangeMemberGuardianCommand(Guid.CreateVersion7(), null))
            .IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(new ChangeMemberGuardianCommand(Guid.Empty, null));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ChangeMemberGuardianCommand.MemberId));
    }

    [Fact]
    public void AGuardianWithABlankName_IsRejectedThroughTheSharedShape()
    {
        var outcome = _validator.Validate(new ChangeMemberGuardianCommand(
            Guid.CreateVersion7(), new GuardianDetails("", "Doinel")));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName.StartsWith(
            nameof(ChangeMemberGuardianCommand.Guardian),
            StringComparison.Ordinal));
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether the removal is *allowed* — never for a child — is the aggregate's invariant,
        // judged against the category the shape cannot see.
        typeof(ChangeMemberGuardianCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
