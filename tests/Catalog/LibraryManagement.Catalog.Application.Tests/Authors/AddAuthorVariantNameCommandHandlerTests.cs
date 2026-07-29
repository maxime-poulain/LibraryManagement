using LibraryManagement.Catalog.Application.Authors.AddAuthorVariantName;
using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class AddAuthorVariantNameCommandHandlerTests
{
    private readonly InMemoryAuthorRepository _authors = new();

    private ValueTask<Result> Handle(AddAuthorVariantNameCommand command)
        => new AddAuthorVariantNameCommandHandler(_authors)
            .Handle(command, TestContext.Current.CancellationToken);

    private static PersonName NameOf(string value)
        => PersonName.Create(value).Match(name => name, _ => throw new InvalidOperationException());

    private static Author AnAuthor(string name = "Vian, Boris")
        => Author.Register(AuthorId.Generate(), NameOf(name), LifeYears.Unknown);

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_RecordsTheVariant()
    {
        // Vian published as Sullivan; a reader who only knows the pen name must still arrive.
        var author = AnAuthor("Vian, Boris");
        _authors.With(author);

        var result = await Handle(new AddAuthorVariantNameCommand(author.Id.Value, "Sullivan, Vernon"));

        result.Match(() => true, _ => false).ShouldBeTrue();
        author.VariantNames.ShouldContain(NameOf("Sullivan, Vernon"));
    }

    [Fact]
    public async Task Handle_AnAuthorNobodyCatalogued_Fails()
    {
        var result = await Handle(new AddAuthorVariantNameCommand(Guid.CreateVersion7(), "Sullivan, Vernon"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.AuthorNotFound);
    }

    [Fact]
    public async Task Handle_AVariantAlreadyRecorded_Fails()
    {
        var author = AnAuthor("Vian, Boris");
        _authors.With(author);
        await Handle(new AddAuthorVariantNameCommand(author.Id.Value, "Sullivan, Vernon"));

        var result = await Handle(new AddAuthorVariantNameCommand(author.Id.Value, "Sullivan, Vernon"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }

    [Fact]
    public async Task Handle_TheHeadingItself_IsRefusedAsAVariant()
    {
        var author = AnAuthor("Vian, Boris");
        _authors.With(author);

        var result = await Handle(new AddAuthorVariantNameCommand(author.Id.Value, "Vian, Boris"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.DuplicateName);
    }
}
