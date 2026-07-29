using LibraryManagement.Catalog.Application.Works.RemoveAuthorCredit;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class RemoveAuthorCreditCommandValidatorTests
{
    private readonly RemoveAuthorCreditCommandValidator _validator = new();

    private static RemoveAuthorCreditCommand ARemoval(Guid? workId = null, Guid? authorId = null)
        => new(workId ?? Guid.CreateVersion7(), authorId ?? Guid.CreateVersion7());

    [Fact]
    public void AWellFormedRemoval_IsAccepted()
    {
        _validator.Validate(ARemoval()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheEmptyWorkIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ARemoval(workId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RemoveAuthorCreditCommand.WorkId));
    }

    [Fact]
    public void TheEmptyAuthorIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ARemoval(authorId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(RemoveAuthorCreditCommand.AuthorId));
    }
}
