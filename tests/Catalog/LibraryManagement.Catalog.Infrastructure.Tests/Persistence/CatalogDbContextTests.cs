using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CatalogDbContextTests(SqlServerFixture sqlServer)
{
    // Touching Model forces EF to build and validate the whole mapping. Every configuration mistake
    // — a converter that cannot round-trip, an owned collection with no key, a property EF cannot
    // reach — surfaces here rather than on the first request in production.
    private IModel Model()
    {
        using var context = sqlServer.NewContext();
        return context.Model;
    }

    [Fact]
    public void TheModel_Builds()
    {
        Should.NotThrow(Model);
    }

    [Fact]
    public void TheModel_MapsEveryAggregate()
    {
        var model = Model();

        model.FindEntityType(typeof(Author)).ShouldNotBeNull();
        model.FindEntityType(typeof(Work)).ShouldNotBeNull();
        model.FindEntityType(typeof(Edition)).ShouldNotBeNull();
    }

    [Fact]
    public async Task EveryTable_ActuallyLivesInTheCatalogSchema()
    {
        // Asked of the database, not of the model. The schema is the module boundary made visible:
        // no foreign key leaves it, and nothing outside it may reach in.
        await using var context = sqlServer.NewContext();

        var schemas = await context.Database
            .SqlQuery<string>(
                $"SELECT DISTINCT TABLE_SCHEMA AS Value FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
            .ToListAsync(TestContext.Current.CancellationToken);

        schemas.ShouldBe([CatalogDbContext.Schema]);
    }

    [Fact]
    public void AnIdentifier_IsStoredAsItsGuidAndNeverGeneratedByTheStore()
    {
        // Identifiers are made by the domain, before anything is written. A store that assigned them
        // would make a caller wait for a round trip to learn what it had just created — which it
        // cannot do, because a command returns no value.
        var id = Model().FindEntityType(typeof(Author))!.FindProperty(nameof(Author.Id))!;

        id.GetValueConverter().ShouldNotBeNull();
        id.GetValueConverter()!.ProviderClrType.ShouldBe(typeof(Guid));
        id.ValueGenerated.ShouldBe(ValueGenerated.Never);
    }

    [Fact]
    public void VariantNames_AreATableOfTheirOwn()
    {
        // Not a JSON column: a variant name is what someone searches by when they only know a name
        // the author no longer uses, and that search wants an index.
        var variants = Model()
            .FindEntityType(typeof(Author))!
            .FindNavigation(nameof(Author.VariantNames))!
            .TargetEntityType;

        variants.GetTableName().ShouldBe("AuthorVariantName");
        variants.IsOwned().ShouldBeTrue();
    }

    [Fact]
    public void AWork_HasNoNavigationToItsAuthors()
    {
        // Two aggregates. A work holds the identity of its authors and never the authors themselves,
        // so no query on a work can drag an Author across with it.
        var work = Model().FindEntityType(typeof(Work))!;

        work.GetNavigations()
            .ShouldNotContain(navigation => navigation.TargetEntityType.ClrType == typeof(Author));
    }

    [Fact]
    public void DomainEvents_AreNotPersisted()
    {
        // They are an intention, raised in memory and dispatched before the work is saved. A column
        // for them would record that something was about to happen.
        var author = Model().FindEntityType(typeof(Author))!;

        author.FindProperty(nameof(Author.DomainEvents)).ShouldBeNull();
        author.FindNavigation(nameof(Author.DomainEvents)).ShouldBeNull();
    }

    [Fact]
    public void EveryAggregate_CarriesAConcurrencyTokenTheEngineMaintains()
    {
        // Two employees acting on the same record at the same moment is expected in a library, and
        // the token is what turns it into a failed Result instead of a silent overwrite. SQL Server
        // maintains a rowversion itself, so no application code can forget to move it.
        foreach (var type in new[] { typeof(Author), typeof(Work), typeof(Edition) })
        {
            var rowVersion = Model().FindEntityType(type)!.FindProperty("RowVersion");

            rowVersion.ShouldNotBeNull();
            rowVersion.IsConcurrencyToken.ShouldBeTrue();
            rowVersion.ValueGenerated.ShouldBe(ValueGenerated.OnAddOrUpdate);
        }
    }
}
