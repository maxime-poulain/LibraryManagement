using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Application.Works.CreditAuthor;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class CreditAuthorCommandHandlerTests
{
    private readonly InMemoryWorkRepository _works = new();
    private readonly InMemoryAuthorRepository _authors = new();

    private ValueTask<Result> Handle(CreditAuthorCommand command)
        => new CreditAuthorCommandHandler(_works, _authors)
            .Handle(command, TestContext.Current.CancellationToken);

    private static Author AnAuthor()
        => Author.Register(
            AuthorId.Generate(),
            NameForm.Create("Guattari, Félix").Match(name => name, _ => throw new InvalidOperationException()),
            LifeYears.Unknown);

    private static Work AWork(params AuthorId[] authorIds)
        => Work.Register(
                WorkId.Generate(),
                Title.Create("Mille plateaux").Match(value => value, _ => throw new InvalidOperationException()),
                authorIds)
            .Match(work => work, _ => throw new InvalidOperationException());

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_CreditsTheAuthor()
    {
        // Attribution moving after cataloging is the ordinary life of a record: the work was
        // registered under one author, and scholarship adds the second.
        var author = AnAuthor();
        var work = AWork();
        _authors.With(author);
        _works.With(work);

        var result = await Handle(new CreditAuthorCommand(work.Id.Value, author.Id.Value));

        result.Match(() => true, _ => false).ShouldBeTrue();
        work.AuthorIds.ShouldContain(author.Id);
    }

    [Fact]
    public async Task Handle_AWorkNobodyCataloged_Fails()
    {
        var author = AnAuthor();
        _authors.With(author);

        var result = await Handle(new CreditAuthorCommand(Guid.CreateVersion7(), author.Id.Value));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.WorkNotFound);
    }

    [Fact]
    public async Task Handle_CreditingAnAuthorThatIsNotCataloged_Fails()
    {
        // The rule spanning two aggregates, asked by the handler here exactly as it is at
        // registration: a credit must point at a record that exists.
        var work = AWork();
        _works.With(work);

        var result = await Handle(new CreditAuthorCommand(work.Id.Value, Guid.CreateVersion7()));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.AuthorNotFound);
        work.AuthorIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_CreditingTheSameAuthorTwice_Fails()
    {
        var author = AnAuthor();
        var work = AWork(author.Id);
        _authors.With(author);
        _works.With(work);

        var result = await Handle(new CreditAuthorCommand(work.Id.Value, author.Id.Value));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateAuthor);
    }
}
