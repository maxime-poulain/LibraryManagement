using LibraryManagement.Catalog.Application.Authors.CorrectAuthorLifeYears;
using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class CorrectAuthorLifeYearsCommandHandlerTests
{
    private readonly InMemoryAuthorRepository _authors = new();

    private ValueTask<Result> Handle(CorrectAuthorLifeYearsCommand command)
        => new CorrectAuthorLifeYearsCommandHandler(_authors)
            .Handle(command, TestContext.Current.CancellationToken);

    private static Author AnAuthor()
        => Author.Register(
            AuthorId.Generate(),
            PersonName.Create("Ernaux, Annie").Match(name => name, _ => throw new InvalidOperationException()),
            LifeYears.Unknown);

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_CorrectsTheYears()
    {
        // The commonest real correction: a record that knew nothing gains its dates.
        var author = AnAuthor();
        _authors.With(author);

        var result = await Handle(new CorrectAuthorLifeYearsCommand(author.Id.Value, 1940, null));

        result.Match(() => true, _ => false).ShouldBeTrue();
        author.LifeYears.Birth.ShouldBe(1940);
    }

    [Fact]
    public async Task Handle_AnAuthorNobodyCatalogued_Fails()
    {
        var result = await Handle(new CorrectAuthorLifeYearsCommand(Guid.CreateVersion7(), 1940, null));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.AuthorNotFound);
    }

    [Fact]
    public async Task Handle_WithADeathBeforeTheBirth_FailsAndTouchesNothing()
    {
        var author = AnAuthor();
        _authors.With(author);

        var result = await Handle(new CorrectAuthorLifeYearsCommand(author.Id.Value, 1944, 1900));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidLifeYears);
        author.LifeYears.ShouldBe(LifeYears.Unknown);
    }

    [Fact]
    public async Task Handle_ToUnknownYears_Succeeds()
    {
        // A valid correction of its own: the years turn out not to be known after all.
        var author = AnAuthor();
        _authors.With(author);
        await Handle(new CorrectAuthorLifeYearsCommand(author.Id.Value, 1940, null));

        var result = await Handle(new CorrectAuthorLifeYearsCommand(author.Id.Value, null, null));

        result.Match(() => true, _ => false).ShouldBeTrue();
        author.LifeYears.Birth.ShouldBeNull();
    }
}
