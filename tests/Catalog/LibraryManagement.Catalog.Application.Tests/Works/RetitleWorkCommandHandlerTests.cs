using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Application.Works.RetitleWork;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class RetitleWorkCommandHandlerTests
{
    private readonly InMemoryWorkRepository _works = new();

    private ValueTask<Result> Handle(RetitleWorkCommand command)
        => new RetitleWorkCommandHandler(_works).Handle(command, TestContext.Current.CancellationToken);

    private static Work AWork(string title = "La Horde du Contrevent")
        => Work.Register(
                WorkId.Generate(),
                Title.Create(title).Match(value => value, _ => throw new InvalidOperationException()))
            .Match(work => work, _ => throw new InvalidOperationException());

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_RecordsTheWorkUnderTheNewTitle()
    {
        var work = AWork("La Horde");
        _works.With(work);

        var result = await Handle(new RetitleWorkCommand(work.Id.Value, "La Horde du Contrevent"));

        result.Match(() => true, _ => false).ShouldBeTrue();
        work.PreferredTitle.Value.ShouldBe("La Horde du Contrevent");
    }

    [Fact]
    public async Task Handle_AWorkNobodyCataloged_Fails()
    {
        var result = await Handle(new RetitleWorkCommand(Guid.CreateVersion7(), "La Horde du Contrevent"));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.WorkNotFound);
    }

    [Fact]
    public async Task Handle_WithATitleThatIsNotOne_FailsAndTouchesNothing()
    {
        var work = AWork("La Horde du Contrevent");
        _works.With(work);

        var result = await Handle(new RetitleWorkCommand(work.Id.Value, "   "));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.InvalidTitle);
        work.PreferredTitle.Value.ShouldBe("La Horde du Contrevent");
    }

    [Fact]
    public async Task Handle_RetitlingToTheCurrentTitle_SucceedsAndRecordsNothing()
    {
        // Nothing happened, so nothing is announced: an event here would send the search projection
        // to replace an access point with itself.
        var work = AWork("La Horde du Contrevent");
        _works.With(work);

        var result = await Handle(new RetitleWorkCommand(work.Id.Value, "La Horde du Contrevent"));

        result.Match(() => true, _ => false).ShouldBeTrue();
        work.DomainEvents.ShouldNotContain(domainEvent => domainEvent is WorkRetitled);
    }
}
