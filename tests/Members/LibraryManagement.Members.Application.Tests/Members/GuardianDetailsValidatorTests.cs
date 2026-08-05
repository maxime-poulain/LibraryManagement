using LibraryManagement.Members.Application.Members;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Tests.Members;

/// <summary>
/// The shared shape of a guardian, tested once the way it is declared once — the two commands
/// that carry one compose this validator rather than repeating its rules.
/// </summary>
public sealed class GuardianDetailsValidatorTests
{
    private readonly GuardianDetailsValidator _validator = new();

    private static GuardianDetails AGuardian(
        string givenName = "Gilberte",
        string familyName = "Doinel",
        string? email = null,
        string? phone = null,
        string? postalAddress = null)
        => new(givenName, familyName, email, phone, postalAddress);

    // --- What a well-formed guardian looks like --------------------------------------------------

    [Fact]
    public void AWellFormedGuardian_IsAccepted()
    {
        _validator.Validate(AGuardian(email: "gilberte.doinel@example.org")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AGuardianWithNoChannelAtAll_IsAccepted()
    {
        // As optional on the guardian as on the member: a guardian without a channel is a
        // work-list case, not an invalid one.
        _validator.Validate(AGuardian()).IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Theory]
    [InlineData("", "Doinel")]
    [InlineData("   ", "Doinel")]
    [InlineData("Gilberte", "")]
    [InlineData("Gilberte", "   ")]
    public void ANameThatIsNotOne_IsRejected(string givenName, string familyName)
    {
        _validator.Validate(AGuardian(givenName, familyName)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ANamePastItsBound_IsRejected()
    {
        var outcome = _validator.Validate(
            AGuardian(familyName: new string('x', MemberName.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(GuardianDetails.FamilyName));
    }

    [Fact]
    public void AChannelPastItsBound_IsRejected()
    {
        var outcome = _validator.Validate(AGuardian(
            email: new string('a', ContactDetails.MaxEmailLength + 1),
            phone: new string('1', ContactDetails.MaxPhoneLength + 1),
            postalAddress: new string('a', ContactDetails.MaxPostalAddressLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(3);
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether a guardian is required at all is judged against the category, by the aggregate.
        typeof(GuardianDetailsValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
