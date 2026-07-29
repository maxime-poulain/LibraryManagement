using LibraryManagement.Catalog.Application.Editions.RegisterEdition;
using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Editions;

public sealed class RegisterEditionCommandHandlerTests
{
    private readonly InMemoryEditionRepository _editions = new();
    private readonly InMemoryWorkRepository _works = new();

    private ValueTask<Result> Handle(RegisterEditionCommand command)
        => new RegisterEditionCommandHandler(_editions, _works)
            .Handle(command, TestContext.Current.CancellationToken);

    private Work ACataloguedWork()
    {
        var work = Work.Register(
                WorkId.Generate(),
                Title.Create("Le Petit Prince").Match(value => value, _ => throw new InvalidOperationException()))
            .Match(created => created, _ => throw new InvalidOperationException());

        _works.Add(work);

        return work;
    }

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_AddsTheEditionToTheStore()
    {
        var work = ACataloguedWork();
        var command = new RegisterEditionCommand(Guid.CreateVersion7(), work.Id.Value, "978-2-07-061275-8");

        var result = await Handle(command);

        result.Match(() => true, _ => false).ShouldBeTrue();
        var edition = _editions.Added.Single();
        edition.Id.Value.ShouldBe(command.EditionId);
        edition.WorkId.ShouldBe(work.Id);
        edition.Isbn.ShouldNotBeNull().Value.ShouldBe("9782070612758");
    }

    [Fact]
    public async Task Handle_WithoutAnIsbn_Succeeds()
    {
        var work = ACataloguedWork();

        var result = await Handle(new RegisterEditionCommand(Guid.CreateVersion7(), work.Id.Value, Isbn: null));

        result.Match(() => true, _ => false).ShouldBeTrue();
        _editions.Added.Single().Isbn.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_AWorkNobodyCatalogued_Fails()
    {
        // The rule spanning two aggregates, asked by the handler exactly as crediting an author
        // asks it: an edition prints a work, and the work must be there to be printed.
        var result = await Handle(new RegisterEditionCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), null));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.WorkNotFound);
        _editions.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WithAValueThatIsNotAnIsbn_Fails()
    {
        var work = ACataloguedWork();

        var result = await Handle(new RegisterEditionCommand(Guid.CreateVersion7(), work.Id.Value, "9782070612757"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidIsbn);
        _editions.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_WithBothWrong_ReportsBothAtOnce()
    {
        // The ISBN and the work's existence are independent; an employee who got both wrong is
        // told both times rather than once per attempt.
        var result = await Handle(new RegisterEditionCommand(Guid.CreateVersion7(), Guid.CreateVersion7(), "not-an-isbn"));

        var codes = ErrorsOf(result).Select(error => error.ErrorCode).ToArray();

        codes.ShouldContain(CatalogErrorCodes.InvalidIsbn);
        codes.ShouldContain(CatalogErrorCodes.WorkNotFound);
    }
}
