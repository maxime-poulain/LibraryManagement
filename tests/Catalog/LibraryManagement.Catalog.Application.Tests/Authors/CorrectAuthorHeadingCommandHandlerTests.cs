using LibraryManagement.Catalog.Application.Authors.CorrectAuthorHeading;
using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class CorrectAuthorHeadingCommandHandlerTests
{
    private readonly InMemoryAuthorRepository _authors = new();

    private ValueTask<Result> Handle(CorrectAuthorHeadingCommand command)
        => new CorrectAuthorHeadingCommandHandler(_authors)
            .Handle(command, TestContext.Current.CancellationToken);

    private static PersonName NameOf(string value)
        => PersonName.Create(value).Match(name => name, _ => throw new InvalidOperationException());

    private static Author AnAuthor(string name = "Hugo, Vicotr")
        => Author.Register(AuthorId.Generate(), NameOf(name), LifeYears.Unknown);

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_RepairsTheHeading()
    {
        var author = AnAuthor("Hugo, Vicotr");
        _authors.With(author);

        var result = await Handle(new CorrectAuthorHeadingCommand(author.Id.Value, "Hugo, Victor"));

        result.Match(() => true, _ => false).ShouldBeTrue();
        author.AuthorizedName.Value.ShouldBe("Hugo, Victor");
    }

    [Fact]
    public async Task Handle_KeepsNothingOfTheWrongForm()
    {
        // The half that tells a correction apart from a rename: a typo is nobody's name, and kept
        // as a variant it would stay a searchable access point forever.
        var author = AnAuthor("Hugo, Vicotr");
        _authors.With(author);

        await Handle(new CorrectAuthorHeadingCommand(author.Id.Value, "Hugo, Victor"));

        author.VariantNames.ShouldNotContain(NameOf("Hugo, Vicotr"));
    }

    [Fact]
    public async Task Handle_AnAuthorNobodyCatalogued_Fails()
    {
        var result = await Handle(new CorrectAuthorHeadingCommand(Guid.CreateVersion7(), "Hugo, Victor"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.AuthorNotFound);
    }

    [Fact]
    public async Task Handle_CorrectingToTheCurrentHeading_Fails()
    {
        var author = AnAuthor("Hugo, Victor");
        _authors.With(author);

        var result = await Handle(new CorrectAuthorHeadingCommand(author.Id.Value, "Hugo, Victor"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }
}
