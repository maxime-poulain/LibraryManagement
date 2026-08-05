using LibraryManagement.Holdings.Application.Copies.AcquireCopy;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Application.Tests.Copies;

public sealed class AcquireCopyCommandValidatorTests
{
    private readonly AcquireCopyCommandValidator _validator = new();

    private static AcquireCopyCommand AnAcquisition(
        Guid? copyId = null,
        Guid? editionId = null,
        string barcode = "30124000512",
        string shelfmark = "843.912 SAI",
        DateOnly? acquiredOn = null,
        CopyCondition condition = CopyCondition.Good)
        => new(
            copyId ?? Guid.CreateVersion7(),
            editionId ?? Guid.CreateVersion7(),
            barcode,
            shelfmark,
            acquiredOn ?? new DateOnly(2024, 3, 14),
            condition);

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedAcquisition_IsAccepted()
    {
        _validator.Validate(AnAcquisition()).IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyCopyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(AnAcquisition(copyId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AcquireCopyCommand.CopyId));
    }

    [Fact]
    public void TheEmptyEditionIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(AnAcquisition(editionId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AcquireCopyCommand.EditionId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    public void ABarcodeShorterThanALabelMayCarry_IsRejected(string barcode)
    {
        var outcome = _validator.Validate(AnAcquisition(barcode: barcode));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AcquireCopyCommand.Barcode));
    }

    [Fact]
    public void ABarcodeLongerThanALabelMayRun_IsRejected()
    {
        var outcome = _validator.Validate(
            AnAcquisition(barcode: new string('1', Barcode.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AcquireCopyCommand.Barcode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AShelfmarkThatIsNotOne_IsRejected(string shelfmark)
    {
        var outcome = _validator.Validate(AnAcquisition(shelfmark: shelfmark));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AcquireCopyCommand.Shelfmark));
    }

    [Fact]
    public void AShelfmarkPastItsBound_IsRejected()
    {
        var outcome = _validator.Validate(
            AnAcquisition(shelfmark: new string('x', Shelfmark.MaxLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AcquireCopyCommand.Shelfmark));
    }

    [Fact]
    public void AnAcquisitionBeforePrinting_IsRejected()
    {
        // The fixed floor, not a clock: an implausible date is a question about the field, and no
        // library in this system acquired anything before printing did.
        var outcome = _validator.Validate(AnAcquisition(
            acquiredOn: AcquireCopyCommandValidator.EarliestAcquisition.AddDays(-1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AcquireCopyCommand.AcquiredOn));
    }

    [Fact]
    public void AConditionOutsideTheSet_IsRejected()
    {
        var outcome = _validator.Validate(AnAcquisition(condition: (CopyCondition)99));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(AcquireCopyCommand.Condition));
    }

    [Fact]
    public void EveryMistakeInARequest_IsReportedAtOnce()
    {
        var outcome = _validator.Validate(
            AnAcquisition(copyId: Guid.Empty, barcode: "1", shelfmark: ""));

        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(3);
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether the barcode is free and whether the edition exists both need a store, and the
        // handler asks — which is how the refusal gets to name the label and the edition.
        typeof(AcquireCopyCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
