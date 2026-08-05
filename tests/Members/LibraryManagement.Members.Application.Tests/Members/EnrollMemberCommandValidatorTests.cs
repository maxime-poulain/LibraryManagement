using LibraryManagement.Members.Application.Members;
using LibraryManagement.Members.Application.Members.EnrollMember;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class EnrollMemberCommandValidatorTests
{
    private readonly EnrollMemberCommandValidator _validator = new();

    private static EnrollMemberCommand AnEnrollment(
        Guid? memberId = null,
        string givenName = "Antoine",
        string familyName = "Doinel",
        DateOnly? dateOfBirth = null,
        MemberCategory category = MemberCategory.Adult,
        string cardNumber = "20260000512",
        string? email = "antoine.doinel@example.org",
        string? phone = null,
        string? postalAddress = null,
        GuardianDetails? guardian = null)
        => new(
            memberId ?? Guid.CreateVersion7(),
            givenName,
            familyName,
            dateOfBirth ?? new DateOnly(1990, 5, 1),
            category,
            cardNumber,
            email,
            phone,
            postalAddress,
            guardian);

    private static string TooLongAName() => new('x', MemberName.MaxLength + 1);

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedEnrollment_IsAccepted()
    {
        _validator.Validate(AnEnrollment()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AMemberWithNoChannelAtAll_IsAccepted()
    {
        // The legitimate state the work list exists for: a member may have no email, no phone and
        // no address, and a shape rule demanding one would validate away a person.
        _validator.Validate(AnEnrollment(email: null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AGuardianOnAnAdult_IsAccepted()
    {
        // A protected adult under tutelle has one too; the child invariant is a floor, not a
        // ceiling, and either way it is the aggregate's to enforce.
        _validator.Validate(AnEnrollment(guardian: new GuardianDetails("Gilberte", "Doinel")))
            .IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(AnEnrollment(memberId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(EnrollMemberCommand.MemberId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AGivenNameThatIsNotOne_IsRejected(string givenName)
    {
        var outcome = _validator.Validate(AnEnrollment(givenName: givenName));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(EnrollMemberCommand.GivenName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AFamilyNameThatIsNotOne_IsRejected(string familyName)
    {
        var outcome = _validator.Validate(AnEnrollment(familyName: familyName));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(EnrollMemberCommand.FamilyName));
    }

    [Fact]
    public void ANameLongerThanANameMayRun_IsRejected()
    {
        var outcome = _validator.Validate(AnEnrollment(givenName: TooLongAName()));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(EnrollMemberCommand.GivenName));
    }

    [Fact]
    public void ABirthDateBeforeTheFloor_IsRejected()
    {
        // The fixed floor, not a clock: whether the date is in the past needs the day in hand,
        // and the aggregate answers that one.
        var outcome = _validator.Validate(AnEnrollment(
            dateOfBirth: EnrollMemberCommandValidator.EarliestBirth.AddDays(-1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(EnrollMemberCommand.DateOfBirth));
    }

    [Fact]
    public void ACategoryOutsideTheSet_IsRejected()
    {
        var outcome = _validator.Validate(AnEnrollment(category: (MemberCategory)99));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(EnrollMemberCommand.Category));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    public void ACardNumberShorterThanACardMayCarry_IsRejected(string cardNumber)
    {
        var outcome = _validator.Validate(AnEnrollment(cardNumber: cardNumber));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(EnrollMemberCommand.CardNumber));
    }

    [Fact]
    public void ACardNumberLongerThanACardMayRun_IsRejected()
    {
        var outcome = _validator.Validate(
            AnEnrollment(cardNumber: new string('1', CardNumber.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(EnrollMemberCommand.CardNumber));
    }

    [Fact]
    public void AChannelPastItsBound_IsRejected()
    {
        var outcome = _validator.Validate(AnEnrollment(
            email: new string('a', ContactDetails.MaxEmailLength + 1),
            phone: new string('1', ContactDetails.MaxPhoneLength + 1),
            postalAddress: new string('a', ContactDetails.MaxPostalAddressLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void AGuardianWithABlankName_IsRejectedThroughTheSharedShape()
    {
        // The guardian's shape is declared once, in GuardianDetailsValidator, and composed here —
        // the nested property name is what proves the composition actually ran.
        var outcome = _validator.Validate(AnEnrollment(guardian: new GuardianDetails(" ", "Doinel")));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName.StartsWith(
            nameof(EnrollMemberCommand.Guardian),
            StringComparison.Ordinal));
    }

    [Fact]
    public void EveryMistakeInARequest_IsReportedAtOnce()
    {
        var outcome = _validator.Validate(
            AnEnrollment(memberId: Guid.Empty, givenName: "", cardNumber: "1"));

        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(3);
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether the card number is free needs the store; whether the birth date is in the past
        // needs the day in hand; whether a child has a guardian is the aggregate's invariant. All
        // three are refused downstream, where the refusal can say more than "invalid".
        typeof(EnrollMemberCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
