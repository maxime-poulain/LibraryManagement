using LibraryManagement.Members.Application.Members.RenameMember;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class RenameMemberCommandValidatorTests
{
    private readonly RenameMemberCommandValidator _validator = new();

    private static RenameMemberCommand ARenaming(
        Guid? memberId = null,
        string givenName = "Antoine",
        string familyName = "Doinel-Montag")
        => new(memberId ?? Guid.CreateVersion7(), givenName, familyName);

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedRenaming_IsAccepted()
    {
        _validator.Validate(ARenaming()).IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ARenaming(memberId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RenameMemberCommand.MemberId));
    }

    [Theory]
    [InlineData("", "Doinel")]
    [InlineData("   ", "Doinel")]
    [InlineData("Antoine", "")]
    [InlineData("Antoine", "   ")]
    public void ANameThatIsNotOne_IsRejected(string givenName, string familyName)
    {
        _validator.Validate(ARenaming(givenName: givenName, familyName: familyName))
            .IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ANamePastItsBound_IsRejected()
    {
        var outcome = _validator.Validate(
            ARenaming(familyName: new string('x', MemberName.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RenameMemberCommand.FamilyName));
    }

    [Fact]
    public void EveryMistakeInARequest_IsReportedAtOnce()
    {
        var outcome = _validator.Validate(ARenaming(memberId: Guid.Empty, givenName: ""));

        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(2);
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // One operation for the marriage and the typo alike — there is no distinction for a rule
        // to sit on, and nothing here needs a store.
        typeof(RenameMemberCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
