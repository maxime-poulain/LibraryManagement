using LibraryManagement.Members.Application.Members.UpdateMemberContactDetails;
using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class UpdateMemberContactDetailsCommandValidatorTests
{
    private readonly UpdateMemberContactDetailsCommandValidator _validator = new();

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedUpdate_IsAccepted()
    {
        _validator.Validate(new UpdateMemberContactDetailsCommand(
                Guid.CreateVersion7(), "antoine.doinel@example.org", "0612345678", "12 rue des Martyrs"))
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ClearingEveryChannel_IsAccepted()
    {
        // The removal is a legitimate command: a member with no channel is a work-list case, not
        // an invalid one.
        _validator.Validate(new UpdateMemberContactDetailsCommand(Guid.CreateVersion7()))
            .IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(new UpdateMemberContactDetailsCommand(Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(UpdateMemberContactDetailsCommand.MemberId));
    }

    [Fact]
    public void EveryChannelPastItsBound_IsReportedAtOnce()
    {
        var outcome = _validator.Validate(new UpdateMemberContactDetailsCommand(
            Guid.CreateVersion7(),
            new string('a', ContactDetails.MaxEmailLength + 1),
            new string('1', ContactDetails.MaxPhoneLength + 1),
            new string('a', ContactDetails.MaxPostalAddressLength + 1)));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(3);
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether an address that looks right actually delivers is an operational fact the day a
        // message is sent — the model does not parse a channel, and neither does its validator.
        typeof(UpdateMemberContactDetailsCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
