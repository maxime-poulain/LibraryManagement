using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Tests.Editions;

/// <summary>
/// The four rules that decide whether two records may be joined.
/// </summary>
/// <remarks>
/// A real instance and not a double: the abstraction exists so the handler can be given a
/// collaborator, not so this can be faked — there is nothing to simulate in logic that reaches no
/// store, and a stub here would assert the stub.
/// </remarks>
public sealed class EditionMergeDomainServiceTests
{
    // The concrete type, not the abstraction: nothing here resolves a collaborator, so naming the
    // interface would only cost a virtual call and say something untrue about what is under test.
    private readonly EditionMergeDomainService _merge = new();

    private static Edition AnEdition(WorkId? workId = null)
        => Edition.Register(EditionId.Generate(), workId ?? WorkId.Generate(), isbn: null);

    private static IReadOnlyErrorCollection ErrorsOf(Result result)
        => result.Match(() => throw new InvalidOperationException("Expected a failure."), errors => errors);

    private static bool Succeeded(Result result) => result.Match(() => true, _ => false);

    [Fact]
    public void Merge_PointsTheAbsorbedRecordAtItsSurvivor()
    {
        var work = WorkId.Generate();
        var absorbed = AnEdition(work);
        var surviving = AnEdition(work);

        Succeeded(_merge.Merge(absorbed, surviving)).ShouldBeTrue();

        absorbed.AbsorbedInto.ShouldBe(surviving.Id);
    }

    [Fact]
    public void Merge_RaisesTheEventEveryDownstreamContextWaitsOn()
    {
        var work = WorkId.Generate();
        var absorbed = AnEdition(work);
        var surviving = AnEdition(work);

        _merge.Merge(absorbed, surviving);

        var merged = absorbed.DomainEvents.OfType<EditionsMerged>().Single();
        merged.AbsorbedEditionId.ShouldBe(absorbed.Id);
        merged.SurvivingEditionId.ShouldBe(surviving.Id);
    }

    [Fact]
    public void Merge_LeavesTheSurvivorUntouched()
    {
        // Two records are read; one is written. That is what keeps a merge inside the unit of
        // consistency the command already was.
        var work = WorkId.Generate();
        var absorbed = AnEdition(work);
        var surviving = AnEdition(work);

        _merge.Merge(absorbed, surviving);

        surviving.AbsorbedInto.ShouldBeNull();
        surviving.DomainEvents.OfType<EditionsMerged>().ShouldBeEmpty();
    }

    [Fact]
    public void Merge_ARecordIntoItself_IsRefused()
    {
        var edition = AnEdition();

        var errors = ErrorsOf(_merge.Merge(edition, edition));

        // Alone, and not alongside the others: every rule below reads as nonsense about one record
        // compared with itself, and reporting them would describe a mistake nobody made.
        errors.ShouldHaveSingleItem()
            .ErrorCode.ShouldBe(CatalogErrorCodes.EditionCannotAbsorbItself);
        edition.AbsorbedInto.ShouldBeNull();
    }

    [Fact]
    public void Merge_ARecordAlreadyAbsorbed_IsRefused()
    {
        // Neither silence nor a chain: a record absorbed twice would leave whoever holds its
        // identifier pointing at a pointer, and a consumer repoints once from one fact.
        var work = WorkId.Generate();
        var absorbed = AnEdition(work);
        var surviving = AnEdition(work);
        _merge.Merge(absorbed, AnEdition(work));

        var errors = ErrorsOf(_merge.Merge(absorbed, surviving));

        errors.ShouldContain(error => error.ErrorCode == CatalogErrorCodes.EditionAlreadyAbsorbed);
        absorbed.AbsorbedInto.ShouldNotBe(surviving.Id);
    }

    [Fact]
    public void Merge_IntoARecordThatWasItselfAbsorbed_IsRefused()
    {
        var work = WorkId.Generate();
        var absorbed = AnEdition(work);
        var surviving = AnEdition(work);
        _merge.Merge(surviving, AnEdition(work));

        var errors = ErrorsOf(_merge.Merge(absorbed, surviving));

        errors.ShouldContain(error => error.ErrorCode == CatalogErrorCodes.EditionAlreadyAbsorbed);
        absorbed.AbsorbedInto.ShouldBeNull();
    }

    [Fact]
    public void Merge_EditionsOfDifferentWorks_IsRefused()
    {
        // The guard against the one mistake nothing recovers: this would tell Holdings to repoint
        // copies onto an edition of another book, and nothing recorded which ones moved.
        var absorbed = AnEdition();
        var surviving = AnEdition();

        var errors = ErrorsOf(_merge.Merge(absorbed, surviving));

        errors.ShouldContain(error => error.ErrorCode == CatalogErrorCodes.EditionsPrintDifferentWorks);
        absorbed.AbsorbedInto.ShouldBeNull();
        absorbed.DomainEvents.OfType<EditionsMerged>().ShouldBeEmpty();
    }

    [Fact]
    public void Merge_ReportsEveryRefusalAndNotOnlyTheFirst()
    {
        // A cataloger looking at two records that print different works, one of which was already
        // merged away, is better served learning both than making two trips.
        var absorbed = AnEdition();
        var surviving = AnEdition();
        _merge.Merge(surviving, AnEdition(surviving.WorkId));

        var errors = ErrorsOf(_merge.Merge(absorbed, surviving));

        errors.Count.ShouldBe(2);
        errors.ShouldContain(error => error.ErrorCode == CatalogErrorCodes.EditionAlreadyAbsorbed);
        errors.ShouldContain(error => error.ErrorCode == CatalogErrorCodes.EditionsPrintDifferentWorks);
    }

    [Fact]
    public void Merge_DemandsBothRecords()
    {
        Should.Throw<ArgumentNullException>(() => _merge.Merge(null!, AnEdition()));
        Should.Throw<ArgumentNullException>(() => _merge.Merge(AnEdition(), null!));
    }
}
