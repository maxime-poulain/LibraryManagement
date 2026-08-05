using LibraryManagement.Members.Application.Members.ReplaceMemberCard;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class ReplaceMemberCardCommandValidatorTests
{
    private readonly ReplaceMemberCardCommandValidator _validator = new();

    private static ReplaceMemberCardCommand AReplacement(
        Guid? memberId = null,
        string cardNumber = "20260000999")
        => new(memberId ?? Guid.CreateVersion7(), cardNumber);

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedReplacement_IsAccepted()
    {
        _validator.Validate(AReplacement()).IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(AReplacement(memberId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ReplaceMemberCardCommand.MemberId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    public void ACardNumberShorterThanACardMayCarry_IsRejected(string cardNumber)
    {
        var outcome = _validator.Validate(AReplacement(cardNumber: cardNumber));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ReplaceMemberCardCommand.CardNumber));
    }

    [Fact]
    public void ACardNumberLongerThanACardMayRun_IsRejected()
    {
        var outcome = _validator.Validate(
            AReplacement(cardNumber: new string('1', CardNumber.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(ReplaceMemberCardCommand.CardNumber));
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether the number is already on another card needs the store, and the handler asks —
        // which is how the refusal gets to name the number instead of saying "invalid".
        typeof(ReplaceMemberCardCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
