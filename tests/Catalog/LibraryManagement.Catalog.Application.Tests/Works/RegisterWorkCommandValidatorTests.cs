using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class RegisterWorkCommandValidatorTests
{
    private readonly RegisterWorkCommandValidator _validator = new();

    private static RegisterWorkCommand ARegistration(
        Guid? workId = null,
        string title = "Le Petit Prince",
        IReadOnlyList<Guid>? authorIds = null)
        => new(workId ?? Guid.CreateVersion7(), title, authorIds ?? [Guid.CreateVersion7()]);

    private static string TooLongATitle() => new('x', Title.MaxLength + 1);

    // --- What a well-formed request looks like ---------------------------------------------------

    [Fact]
    public void AWellFormedRegistration_IsAccepted()
    {
        _validator.Validate(ARegistration()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void AWorkCreditedToNobody_IsAccepted()
    {
        // Anonymous works, traditional tales and many mediaeval texts have no author, and a rule
        // demanding one would force a librarian to invent one for Le Roman de Renart. The empty list
        // is the case being accepted here; a null one is not, and is rejected below.
        _validator.Validate(ARegistration(authorIds: [])).IsValid.ShouldBeTrue();
    }

    // --- The shape it refuses --------------------------------------------------------------------

    [Fact]
    public void TheEmptyIdentifier_IsRejected()
    {
        var outcome = _validator.Validate(ARegistration(workId: Guid.Empty));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterWorkCommand.WorkId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ATitleThatIsNotOne_IsRejected(string title)
    {
        var outcome = _validator.Validate(ARegistration(title: title));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterWorkCommand.PreferredTitle));
    }

    [Fact]
    public void ATitleLongerThanATitleMayBe_IsRejected()
    {
        var outcome = _validator.Validate(ARegistration(title: TooLongATitle()));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterWorkCommand.PreferredTitle));
    }

    [Fact]
    public void NoListOfAuthorsAtAll_IsRejected()
    {
        // The distinction the command's own documentation draws: an empty list says "nobody wrote
        // this", a missing one says nothing at all. Accepting null would let a caller who forgot the
        // field catalogue an anonymous work without meaning to.
        //
        // Built here rather than through the helper, whose default would put the missing list back.
        var outcome = _validator.Validate(
            new RegisterWorkCommand(Guid.CreateVersion7(), "Le Petit Prince", null!));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterWorkCommand.AuthorIds));
    }

    [Fact]
    public void TheEmptyIdentifierAmongTheAuthors_IsRejected()
    {
        // For the reason the author's own validator gives: AuthorId.Create throws on it, and the
        // handler converts every credit before anything else happens.
        var outcome = _validator.Validate(ARegistration(authorIds: [Guid.CreateVersion7(), Guid.Empty]));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(
            error => error.PropertyName.StartsWith(
                nameof(RegisterWorkCommand.AuthorIds),
                StringComparison.Ordinal));
    }

    [Fact]
    public void TheSameAuthorCreditedTwice_IsRejected()
    {
        // Work.CreditAuthor refuses the duplicate too, and would report it. Catching it here reports
        // it alongside every other malformation instead of one round trip later, and it is a
        // statement about the request rather than about the catalogue.
        var authorId = Guid.CreateVersion7();

        var outcome = _validator.Validate(ARegistration(authorIds: [authorId, authorId]));

        outcome.IsValid.ShouldBeFalse();
        outcome.Errors.ShouldContain(error => error.PropertyName == nameof(RegisterWorkCommand.AuthorIds));
    }

    [Fact]
    public void EveryMistakeInARequest_IsReportedAtOnce()
    {
        var outcome = _validator.Validate(ARegistration(workId: Guid.Empty, title: ""));

        outcome.Errors.Select(error => error.PropertyName).Distinct().Count().ShouldBe(2);
    }

    // --- What it deliberately leaves to someone else ---------------------------------------------

    [Fact]
    public void TheValidator_ChecksTheShapeAndNothingElse()
    {
        // Whether the credited authors exist is a question about the catalogue, needs the store to
        // answer, and the handler asks it — which is also how the answer gets to name the author that
        // is missing.
        typeof(RegisterWorkCommandValidator)
            .GetConstructors()
            .Single()
            .GetParameters()
            .ShouldBeEmpty();
    }
}
