using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using static LibraryManagement.Catalog.Domain.Tests.Catalog;

namespace LibraryManagement.Catalog.Domain.Tests.Works;

public sealed class WorkTests
{
    private static Work AWork(params AuthorId[] authors)
        => Work.Register(WorkId.Generate(), TitleOf("Mille plateaux"), authors)
            .Match(work => work, errors => throw new InvalidOperationException(errors[0].ToString()));

    [Fact]
    public void Register_RecordsTheTitleAndTheAuthorsInOrder()
    {
        var first = AuthorId.Generate();
        var second = AuthorId.Generate();

        var work = AWork(first, second);

        work.PreferredTitle.ShouldBe(TitleOf("Mille plateaux"));
        work.AuthorIds.ShouldBe([first, second]);
    }

    [Fact]
    public void Register_WithNoAuthorAtAll_Succeeds()
    {
        // Anonymous works, traditional tales and many medieval texts have none. Demanding an author
        // would force a librarian to invent one for Le Roman de Renart.
        AWork().AuthorIds.ShouldBeEmpty();
    }

    [Fact]
    public void Register_CreditingTheSameAuthorTwice_Fails()
    {
        var author = AuthorId.Generate();

        var result = Work.Register(WorkId.Generate(), TitleOf("Mille plateaux"), [author, author]);

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateAuthor);
    }

    [Fact]
    public void Register_RaisesTheEventThatSaysSo()
    {
        AWork().DomainEvents.OfType<WorkRegistered>().Single()
            .PreferredTitle.ShouldBe(TitleOf("Mille plateaux"));
    }

    [Fact]
    public void CreditAuthor_AddsOne()
    {
        var work = AWork();
        var author = AuthorId.Generate();

        Succeeded(work.CreditAuthor(author)).ShouldBeTrue();

        work.AuthorIds.ShouldBe([author]);
    }

    [Fact]
    public void CreditAuthor_Twice_IsRefused()
    {
        var author = AuthorId.Generate();
        var work = AWork(author);

        ErrorsOf(work.CreditAuthor(author)).Single().ErrorCode
            .ShouldBe(CatalogErrorCodes.DuplicateAuthor);

        work.AuthorIds.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveAuthorCredit_ReportsWhetherThereWasOne()
    {
        var author = AuthorId.Generate();
        var work = AWork(author);

        work.RemoveAuthorCredit(author).ShouldBeTrue();
        work.RemoveAuthorCredit(author).ShouldBeFalse();
        work.AuthorIds.ShouldBeEmpty();
    }

    [Fact]
    public void Retitle_ChangesTheTitleAndNothingElse()
    {
        var author = AuthorId.Generate();
        var work = AWork(author);

        work.Retitle(TitleOf("A Thousand Plateaus"));

        work.PreferredTitle.ShouldBe(TitleOf("A Thousand Plateaus"));
        work.AuthorIds.ShouldBe([author]);
    }

    [Fact]
    public void Retitle_AnnouncesBothTitles()
    {
        // The projection replaces one access point with the other, so it needs the outgoing title
        // as much as the incoming one.
        var work = AWork();

        work.Retitle(TitleOf("A Thousand Plateaus"));

        var retitled = work.DomainEvents.OfType<WorkRetitled>().Single();
        retitled.PreviousTitle.ShouldBe(TitleOf("Mille plateaux"));
        retitled.NewTitle.ShouldBe(TitleOf("A Thousand Plateaus"));
    }

    [Fact]
    public void Retitle_ToTheCurrentTitle_RecordsNothing()
    {
        // Nothing happened, and an event saying otherwise would send the projection to replace an
        // access point with itself.
        var work = AWork();

        work.Retitle(TitleOf("Mille plateaux"));

        work.DomainEvents.OfType<WorkRetitled>().ShouldBeEmpty();
    }

    [Fact]
    public void AWork_HoldsIdentifiersAndNeverAuthors()
    {
        // Two aggregates. Were an Author reachable from a Work, loading one would drag the other
        // across and a change to either could be made against a stale view of the other.
        typeof(Work).GetProperties()
            .ShouldNotContain(property => property.PropertyType == typeof(Author));

        AWork().AuthorIds.ShouldBeAssignableTo<IReadOnlyList<AuthorId>>();
    }
}
