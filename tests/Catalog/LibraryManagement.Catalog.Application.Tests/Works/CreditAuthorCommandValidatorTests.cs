using LibraryManagement.Catalog.Application.Works.CreditAuthor;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class CreditAuthorCommandValidatorTests
{
    private readonly CreditAuthorCommandValidator _validator = new();

    private static CreditAuthorCommand ACredit(Guid? workId = null, Guid? authorId = null)
        => new(workId ?? Guid.CreateVersion7(), authorId ?? Guid.CreateVersion7());

    [Fact]
    public void AWellFormedCredit_IsAccepted()
    {
        _validator.Validate(ACredit()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void TheEmptyWorkIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ACredit(workId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(CreditAuthorCommand.WorkId));
    }

    [Fact]
    public void TheEmptyAuthorIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ACredit(authorId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName == nameof(CreditAuthorCommand.AuthorId));
    }

    [Fact]
    public void BothIdentifiersMissing_AreReportedAtOnce()
    {
        var outcome = _validator.Validate(ACredit(workId: Guid.Empty, authorId: Guid.Empty));

        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(2);
    }
}
