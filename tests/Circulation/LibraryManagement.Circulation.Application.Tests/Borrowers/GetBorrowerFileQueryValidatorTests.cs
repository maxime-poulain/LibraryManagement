using LibraryManagement.Circulation.Application.Borrowers.GetBorrowerFile;

namespace LibraryManagement.Circulation.Application.Tests.Borrowers;

public sealed class GetBorrowerFileQueryValidatorTests
{
    private readonly GetBorrowerFileQueryValidator _validator = new();

    [Fact]
    public void AQueryCarryingAnIdentifier_IsWellFormed()
    {
        _validator.Validate(new GetBorrowerFileQuery(Guid.CreateVersion7())).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AQueryCarryingTheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(new GetBorrowerFileQuery(Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.Single().PropertyName.ShouldBe(nameof(GetBorrowerFileQuery.BorrowerId));
    }

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // There is nothing else it could check. This context keeps no register of people, so an
        // unknown borrower is not a thing it can refuse — only one with nothing on file, which is
        // an answer and not an error.
        typeof(GetBorrowerFileQueryValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
