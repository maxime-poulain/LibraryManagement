using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Domain.Editions;

namespace LibraryManagement.Catalog.Application.Tests.Editions;

public sealed class RegisterEditionCommandValidatorTests
{
    private readonly RegisterEditionCommandValidator _validator = new();

    private static RegisterEditionCommand ARegistration(
        Guid? editionId = null,
        Guid? workId = null,
        string? isbn = "978-2-07-061275-8")
        => new(editionId ?? Guid.CreateVersion7(), workId ?? Guid.CreateVersion7(), isbn);

    [Fact]
    public void AWellFormedRegistration_IsAccepted()
    {
        _validator.Validate(ARegistration()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ARegistrationWithoutAnIsbn_IsAccepted()
    {
        // Null states a fact — the edition bears none — and is not a missing field.
        _validator.Validate(ARegistration(isbn: null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheEmptyEditionIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ARegistration(editionId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterEditionCommand.EditionId));
    }

    [Fact]
    public void TheEmptyWorkIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ARegistration(workId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterEditionCommand.WorkId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankIsbn_IsRejected(string isbn)
    {
        // An absent ISBN is null; a blank one is a malformed request, not a statement of absence.
        var outcome = _validator.Validate(ARegistration(isbn: isbn));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterEditionCommand.Isbn));
    }

    [Fact]
    public void AnIsbnLongerThanAnyWrittenForm_IsRejected()
    {
        var outcome = _validator.Validate(ARegistration(isbn: new string('9', Isbn.LongestWrittenForm + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RegisterEditionCommand.Isbn));
    }

    [Fact]
    public void TheCheckDigit_IsNotThisValidatorsBusiness()
    {
        // Well-formed and untrue: the digits are digits, the length is a length. Whether they form
        // an ISBN belongs to the value object, and the handler test shows the command failing on
        // the domain's answer instead.
        _validator.Validate(ARegistration(isbn: "9782070612757")).IsValid.ShouldBeTrue();
    }
}
