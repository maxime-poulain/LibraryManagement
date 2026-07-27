using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Authors;

public sealed class RegisterAuthorCommandHandlerTests
{
    private readonly InMemoryAuthorRepository _authors = new();

    private ValueTask<Result> Handle(RegisterAuthorCommand command)
        => new RegisterAuthorCommandHandler(_authors).Handle(command, TestContext.Current.CancellationToken);

    private static RegisterAuthorCommand ACommand(
        string name = "Ernaux, Annie",
        int? birth = 1940,
        int? death = null)
        => new(Guid.CreateVersion7(), name, birth, death);

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_AddsTheAuthorToTheStore()
    {
        var command = ACommand();

        var result = await Handle(command);

        result.Match(() => true, _ => false).ShouldBeTrue();
        _authors.Added.Single().AuthorizedName.Value.ShouldBe("Ernaux, Annie");
    }

    [Fact]
    public async Task Handle_KeepsTheIdentifierTheCallerChose()
    {
        // A command returns no value, so the caller has nothing else to go on. Generating the
        // identifier here instead would leave it unable to name what it just created.
        var command = ACommand();

        await Handle(command);

        _authors.Added.Single().Id.Value.ShouldBe(command.AuthorId);
    }

    [Fact]
    public async Task Handle_WithAnEmptyName_Fails()
    {
        var result = await Handle(ACommand(name: "  "));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidPersonName);
        _authors.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WithADeathBeforeTheBirth_Fails()
    {
        var result = await Handle(ACommand(birth: 1944, death: 1900));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidLifeYears);
    }

    [Fact]
    public async Task Handle_WithBothWrong_ReportsBothAtOnce()
    {
        // The name and the years are independent. An employee who got both wrong should be told
        // both times, not once per attempt.
        var result = await Handle(ACommand(name: "", birth: 1944, death: 1900));

        var codes = ErrorsOf(result).Select(error => error.ErrorCode).ToArray();

        codes.ShouldContain(CatalogErrorCodes.InvalidPersonName);
        codes.ShouldContain(CatalogErrorCodes.InvalidLifeYears);
    }

    [Fact]
    public async Task Handle_WhenItFails_AddsNothing()
    {
        await Handle(ACommand(name: ""));

        _authors.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WithUnknownYears_Succeeds()
    {
        var result = await Handle(ACommand(birth: null, death: null));

        result.Match(() => true, _ => false).ShouldBeTrue();
        _authors.Added.Single().LifeYears.Birth.ShouldBeNull();
    }
}
