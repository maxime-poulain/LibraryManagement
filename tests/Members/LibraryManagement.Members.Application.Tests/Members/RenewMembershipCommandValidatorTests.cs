using LibraryManagement.Members.Application.Members.RenewMembership;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class RenewMembershipCommandValidatorTests
{
    private readonly RenewMembershipCommandValidator _validator = new();

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedRenewal_IsAccepted()
    {
        _validator.Validate(new RenewMembershipCommand(Guid.CreateVersion7()))
            .IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(new RenewMembershipCommand(Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RenewMembershipCommand.MemberId));
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // The identifier is the whole command: a renewal is always granted, which of the two
        // arithmetics applies is the calendar's choice, and both belong to the aggregate.
        typeof(RenewMembershipCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
