using LibraryManagement.Charges.Application.Accounts.GetMemberBalance;

namespace LibraryManagement.Charges.Application.Tests.Accounts;

public sealed class GetMemberBalanceQueryValidatorTests
{
    private readonly GetMemberBalanceQueryValidator _validator = new();

    [Fact]
    public void AQueryCarryingAnIdentifier_IsWellFormed()
    {
        _validator.Validate(new GetMemberBalanceQuery(Guid.CreateVersion7())).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AQueryCarryingTheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(new GetMemberBalanceQuery(Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.Single().PropertyName.ShouldBe(nameof(GetMemberBalanceQuery.MemberId));
    }

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether the member exists is Members' business. An account is keyed by an identifier
        // this context never issued, which is exactly what keeps a charge countable after the
        // person behind it has been erased.
        typeof(GetMemberBalanceQueryValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
