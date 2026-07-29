using LibraryManagement.Catalog.Application.Authors.RenameAuthor;
using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class RenameAuthorCommandHandlerTests
{
    private readonly InMemoryAuthorRepository _authors = new();

    private ValueTask<Result> Handle(RenameAuthorCommand command)
        => new RenameAuthorCommandHandler(_authors).Handle(command, TestContext.Current.CancellationToken);

    private static PersonName NameOf(string value)
        => PersonName.Create(value).Match(name => name, _ => throw new InvalidOperationException());

    private static Author AnAuthor(string name = "Ernaux, Annie")
        => Author.Register(AuthorId.Generate(), NameOf(name), LifeYears.Unknown);

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_FilesTheAuthorUnderTheNewName()
    {
        var author = AnAuthor("Ernaux, Annie");
        _authors.With(author);

        var result = await Handle(new RenameAuthorCommand(author.Id.Value, "Duchesne, Annie"));

        result.Match(() => true, _ => false).ShouldBeTrue();
        author.AuthorizedName.Value.ShouldBe("Duchesne, Annie");
    }

    [Fact]
    public async Task Handle_KeepsTheOutgoingNameFindable()
    {
        // The half that tells a rename apart from a correction: the old name stays a variant,
        // because a book printed under it still bears it on its title page.
        var author = AnAuthor("Ernaux, Annie");
        _authors.With(author);

        await Handle(new RenameAuthorCommand(author.Id.Value, "Duchesne, Annie"));

        author.VariantNames.ShouldContain(NameOf("Ernaux, Annie"));
    }

    [Fact]
    public async Task Handle_AnAuthorNobodyCatalogued_Fails()
    {
        var result = await Handle(new RenameAuthorCommand(Guid.CreateVersion7(), "Duchesne, Annie"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.AuthorNotFound);
    }

    [Fact]
    public async Task Handle_WithANameThatIsNotOne_FailsAndTouchesNothing()
    {
        var author = AnAuthor("Ernaux, Annie");
        _authors.With(author);

        var result = await Handle(new RenameAuthorCommand(author.Id.Value, "   "));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidPersonName);
        author.AuthorizedName.Value.ShouldBe("Ernaux, Annie");
    }

    [Fact]
    public async Task Handle_RenamingToTheCurrentName_Fails()
    {
        // The domain's refusal, reported through the handler untouched.
        var author = AnAuthor("Ernaux, Annie");
        _authors.With(author);

        var result = await Handle(new RenameAuthorCommand(author.Id.Value, "Ernaux, Annie"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }
}
