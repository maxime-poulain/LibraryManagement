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
}
