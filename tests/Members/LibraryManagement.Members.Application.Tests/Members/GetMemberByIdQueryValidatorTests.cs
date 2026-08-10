using LibraryManagement.Members.Application.Members.GetMemberById;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class GetMemberByIdQueryValidatorTests
{
    private readonly GetMemberByIdQueryValidator _validator = new();

    [Fact]
    public void AQueryCarryingAnIdentifier_IsWellFormed()
    {
        _validator.Validate(new GetMemberByIdQuery(Guid.CreateVersion7())).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AQueryCarryingTheEmptyIdentifier_IsRejected()
    {
        // What keeps "you asked badly" apart from "nobody is enrolled under that". Without it the
        // handler would answer MemberNotFound to a request that never named a member.
        var outcome = _validator.Validate(new GetMemberByIdQuery(Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.Single().PropertyName.ShouldBe(nameof(GetMemberByIdQuery.MemberId));
    }

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        typeof(GetMemberByIdQueryValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
