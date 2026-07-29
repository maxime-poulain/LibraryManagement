using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Application.Works.RemoveAuthorCredit;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Works;

public sealed class RemoveAuthorCreditCommandHandlerTests
{
    private readonly InMemoryWorkRepository _works = new();

    private ValueTask<Result> Handle(RemoveAuthorCreditCommand command)
        => new RemoveAuthorCreditCommandHandler(_works)
            .Handle(command, TestContext.Current.CancellationToken);

    private static Work AWork(params AuthorId[] authorIds)
        => Work.Register(
                WorkId.Generate(),
                Title.Create("Mille plateaux").Match(value => value, _ => throw new InvalidOperationException()),
                authorIds)
            .Match(work => work, _ => throw new InvalidOperationException());

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    [Fact]
    public async Task Handle_WithdrawsTheCredit()
    {
        // The work ends with no author at all, and that is allowed for the same reason anonymous
        // works exist: better no author than an invented one.
        var authorId = AuthorId.Generate();
        var work = AWork(authorId);
        _works.With(work);

        var result = await Handle(new RemoveAuthorCreditCommand(work.Id.Value, authorId.Value));

        result.Match(() => true, _ => false).ShouldBeTrue();
        work.AuthorIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_AWorkNobodyCatalogued_Fails()
    {
        var result = await Handle(new RemoveAuthorCreditCommand(Guid.CreateVersion7(), Guid.CreateVersion7()));

        ErrorsOf(result).Single().ErrorCode.ShouldBe(CatalogErrorCodes.WorkNotFound);
    }

    [Fact]
    public async Task Handle_AnAuthorNeverCredited_SucceedsAllTheSame()
    {
        // The domain answers with a fact — never credited — and a fact is not a refusal. The
        // command asks for a state, and the state already holds; a failure here would be a rule
        // the domain declined to state.
        var work = AWork();
        _works.With(work);

        var result = await Handle(new RemoveAuthorCreditCommand(work.Id.Value, Guid.CreateVersion7()));

        result.Match(() => true, _ => false).ShouldBeTrue();
    }
}
