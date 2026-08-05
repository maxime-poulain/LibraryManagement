using LibraryManagement.Members.Application.Members.ChangeMemberCategory;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class ChangeMemberCategoryCommandValidatorTests
{
    private readonly ChangeMemberCategoryCommandValidator _validator = new();

    // --- What a well-formed request looks like ---------------------------------------------------

    [Theory]
    [InlineData(MemberCategory.Adult)]
    [InlineData(MemberCategory.Child)]
    [InlineData(MemberCategory.Student)]
    public void EveryCategoryInTheSet_IsAccepted(MemberCategory category)
    {
        _validator.Validate(new ChangeMemberCategoryCommand(Guid.CreateVersion7(), category))
            .IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(
            new ChangeMemberCategoryCommand(Guid.Empty, MemberCategory.Adult));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ChangeMemberCategoryCommand.MemberId));
    }

    [Fact]
    public void ACategoryOutsideTheSet_IsRejected()
    {
        var outcome = _validator.Validate(
            new ChangeMemberCategoryCommand(Guid.CreateVersion7(), (MemberCategory)99));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ChangeMemberCategoryCommand.Category));
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // That entering Child requires a guardian on the record is the aggregate's invariant,
        // judged against state the shape cannot see.
        typeof(ChangeMemberCategoryCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
