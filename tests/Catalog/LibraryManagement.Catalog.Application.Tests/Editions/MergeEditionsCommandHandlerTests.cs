using LibraryManagement.Catalog.Application.Editions.MergeEditions;
using LibraryManagement.Catalog.Application.Tests.TestDoubles;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Application.Tests.Editions;

public sealed class MergeEditionsCommandHandlerTests
{
    private readonly InMemoryEditionRepository _editions = new();

    // The real service and not a double. What this class checks is that the handler *propagates* a
    // refusal it did not make, and a stub would turn that into an assertion about the stub.
    private readonly EditionMergeDomainService _merge = new();

    private ValueTask<Result> Handle(Guid absorbed, Guid surviving)
        => new MergeEditionsCommandHandler(_editions, _merge)
            .Handle(new MergeEditionsCommand(absorbed, surviving), TestContext.Current.CancellationToken);

    private Edition ACatalogedEdition(WorkId? workId = null)
    {
        var edition = Edition.Register(EditionId.Generate(), workId ?? WorkId.Generate(), isbn: null);
        _editions.Add(edition);
        return edition;
    }

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    [Fact]
    public async Task Handle_AbsorbsOneRecordIntoTheOther()
    {
        var work = WorkId.Generate();
        var absorbed = ACatalogedEdition(work);
        var surviving = ACatalogedEdition(work);

        var result = await Handle(absorbed.Id.Value, surviving.Id.Value);

        Succeeded(result).ShouldBeTrue();
        absorbed.AbsorbedInto.ShouldBe(surviving.Id);
        absorbed.DomainEvents.OfType<EditionsMerged>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_LeavesTheSurvivorUntouched()
    {
        // Only one record changes. The survivor is read to be checked and never written, which is
        // why it raises nothing and why the merge is one aggregate's transaction.
        var work = WorkId.Generate();
        var absorbed = ACatalogedEdition(work);
        var surviving = ACatalogedEdition(work);

        await Handle(absorbed.Id.Value, surviving.Id.Value);

        surviving.AbsorbedInto.ShouldBeNull();
        surviving.DomainEvents.OfType<EditionsMerged>().ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ReportsBothMissingRecordsAtOnce()
    {
        // A cataloger who mistyped one identifier may well have mistyped the other, and finding
        // out one at a time is two trips to the desk.
        var result = await Handle(Guid.CreateVersion7(), Guid.CreateVersion7());

        var errors = ErrorsOf(result);
        errors.Count.ShouldBe(2);
        errors.ShouldAllBe(error => error.ErrorCode == CatalogErrorCodes.EditionNotFound);
    }

    [Fact]
    public async Task Handle_ASurvivorThatWasItselfAbsorbed_IsRefused()
    {
        // Merging into a pointer would leave every downstream identifier pointing at a pointer,
        // and a consumer repoints once from one fact.
        var work = WorkId.Generate();
        var absorbed = ACatalogedEdition(work);
        var surviving = ACatalogedEdition(work);
        var thirdRecord = ACatalogedEdition(work);
        _merge.Merge(surviving, thirdRecord);

        var result = await Handle(absorbed.Id.Value, surviving.Id.Value);

        ErrorsOf(result).ShouldContain(error => error.ErrorCode == CatalogErrorCodes.EditionAlreadyAbsorbed);
        absorbed.AbsorbedInto.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_EditionsOfDifferentWorks_AreRefused()
    {
        // The rule that protects the irreversible mistake: merging these tells Holdings to repoint
        // copies onto an edition of another book, and nothing recorded which ones moved.
        var absorbed = ACatalogedEdition();
        var surviving = ACatalogedEdition();

        var result = await Handle(absorbed.Id.Value, surviving.Id.Value);

        ErrorsOf(result)
            .ShouldContain(error => error.ErrorCode == CatalogErrorCodes.EditionsPrintDifferentWorks);
        absorbed.AbsorbedInto.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_ARecordMergedIntoItself_IsRefusedByTheAggregate()
    {
        var edition = ACatalogedEdition();

        var result = await Handle(edition.Id.Value, edition.Id.Value);

        ErrorsOf(result)
            .ShouldContain(error => error.ErrorCode == CatalogErrorCodes.EditionCannotAbsorbItself);
    }
}
