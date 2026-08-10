using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.PublishedLanguage;

namespace LibraryManagement.Catalog.Infrastructure.Tests.PublishedLanguage;

/// <summary>
/// The surface Catalog publishes to the contexts downstream of it, exercised against a real store.
/// </summary>
/// <remarks>
/// It earns integration tests rather than unit ones because everything that can go wrong here is
/// the store's. The first version of this class compared on <c>Id.Value</c>, which compiles, reads
/// well and does not translate — the key carries a value conversion, so the provider sees a member
/// access on a type it has no column for. Nothing but a real query finds that.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class EditionCatalogTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Exists_AnEditionTheCatalogHolds_IsTrue()
    {
        var edition = Edition.Register(EditionId.Generate(), WorkId.Generate(), isbn: null);

        await using var writing = sqlServer.NewContext();
        writing.Add(edition);
        await writing.SaveChangesAsync(Token);

        await using var reading = sqlServer.NewContext();
        var exists = await new EditionCatalog(reading).ExistsAsync(edition.Id.Value, Token);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Exists_AnEditionNobodyCataloged_IsFalse()
    {
        // What Holdings acts on when it refuses an acquisition, so it is worth proving rather than
        // assuming.
        await using var reading = sqlServer.NewContext();

        var exists = await new EditionCatalog(reading).ExistsAsync(Guid.CreateVersion7(), Token);

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task Exists_AnEditionMergedAway_IsFalse()
    {
        // The row is still there and still holds its identifier — deleting it would orphan the
        // contexts that hold it. It stops *existing* for the one purpose this question serves:
        // attaching a copy to a record a cataloger just merged away would manufacture, one
        // acquisition at a time, exactly the orphan the merge event exists to repair.
        var workId = WorkId.Generate();
        var absorbed = Edition.Register(EditionId.Generate(), workId, isbn: null);
        var surviving = Edition.Register(EditionId.Generate(), workId, isbn: null);

        // Through the domain service, which is the only way in: the aggregate's mutator is internal
        // to its own module. Arranging a merged edition the way production makes one is also the
        // arrangement worth having.
        new EditionMergeDomainService().Merge(absorbed, surviving);

        await using (var writing = sqlServer.NewContext())
        {
            writing.AddRange(absorbed, surviving);
            await writing.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var catalog = new EditionCatalog(reading);

        (await catalog.ExistsAsync(absorbed.Id.Value, Token)).ShouldBeFalse();
        (await catalog.ExistsAsync(surviving.Id.Value, Token)).ShouldBeTrue();
    }
}
