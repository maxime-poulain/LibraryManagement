using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class RegisterWorkCommandHandlerTests
{
    private readonly InMemoryWorkRepository _works = new();
    private readonly InMemoryAuthorRepository _authors = new();

    private ValueTask<Result> Handle(RegisterWorkCommand command)
        => new RegisterWorkCommandHandler(_works, _authors)
            .Handle(command, TestContext.Current.CancellationToken);

    private static Author AnAuthor()
        => Author.Register(
            AuthorId.Generate(),
            PersonName.Create("Deleuze, Gilles").Match(name => name, _ => throw new InvalidOperationException()),
            LifeYears.Unknown);

    private static RegisterWorkCommand ACommand(string title, params Guid[] authorIds)
        => new(Guid.CreateVersion7(), title, authorIds);

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_AddsTheWorkToTheStore()
    {
        var author = AnAuthor();
        _authors.With(author);

        var result = await Handle(ACommand("Mille plateaux", author.Id.Value));

        result.Match(() => true, _ => false).ShouldBeTrue();
        _works.Added.Single().Title.Value.ShouldBe("Mille plateaux");
    }

    [Fact]
    public async Task Handle_WithNoAuthors_Succeeds()
    {
        var result = await Handle(ACommand("Le Roman de Renart"));

        result.Match(() => true, _ => false).ShouldBeTrue();
        _works.Added.Single().AuthorIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_CreditingAnAuthorThatIsNotCatalogued_Fails()
    {
        // A rule spanning two aggregates, so neither can enforce it alone and the handler asks.
        var result = await Handle(ACommand("Mille plateaux", Guid.CreateVersion7()));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.AuthorNotFound);
        _works.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_NamesEveryAuthorThatIsMissing()
    {
        // Which author is missing is the useful part, and a foreign key violation would not say.
        var missing = Guid.CreateVersion7();
        var alsoMissing = Guid.CreateVersion7();

        var result = await Handle(ACommand("Mille plateaux", missing, alsoMissing));

        var messages = ErrorsOf(result).Select(error => error.ErrorMessage).ToArray();

        messages.Length.ShouldBe(2);
        messages.ShouldContain(message => message.Contains(missing.ToString(), StringComparison.Ordinal));
        messages.ShouldContain(message => message.Contains(alsoMissing.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Handle_WithABadTitleAndAMissingAuthor_ReportsBoth()
    {
        var result = await Handle(ACommand("   ", Guid.CreateVersion7()));

        var codes = ErrorsOf(result).Select(error => error.ErrorCode).ToArray();

        codes.ShouldContain(CatalogErrorCodes.InvalidTitle);
        codes.ShouldContain(CatalogErrorCodes.AuthorNotFound);
    }

    [Fact]
    public async Task Handle_CreditingTheSameAuthorTwice_Fails()
    {
        var author = AnAuthor();
        _authors.With(author);

        var result = await Handle(ACommand("Mille plateaux", author.Id.Value, author.Id.Value));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateAuthor);
    }
}
