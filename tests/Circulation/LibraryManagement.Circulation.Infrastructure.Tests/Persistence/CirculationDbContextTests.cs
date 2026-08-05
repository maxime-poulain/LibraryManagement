using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LibraryManagement.Circulation.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CirculationDbContextTests(SqlServerFixture sqlServer)
{
    // Touching Model forces EF to build and validate the whole mapping, so a configuration mistake
    // surfaces here rather than on the first request in production.
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
    public void TheModel_MapsBothAggregates()
    {
        var model = Model();

        model.FindEntityType(typeof(Loan)).ShouldNotBeNull();
        model.FindEntityType(typeof(HoldQueue)).ShouldNotBeNull();
    }

    [Fact]
    public void NoKeyOfAnOwnedCollection_IsLeftForTheEngineToGenerate()
    {
        // A key EF believes it generates is a key EF believes already has a row. An entry added to
        // a collection of an aggregate the store already holds — a hold placed in a queue that
        // exists — arrives with that key filled in, so EF marks it Modified rather than Added and
        // issues an UPDATE that matches nothing. Where the table is nothing but its key, not even
        // that: no statement at all, and the loss is silent.
        var generated = Model().GetEntityTypes()
            .Where(entity => entity.IsOwned())
            .SelectMany(entity => entity.FindPrimaryKey()?.Properties ?? [])
            .Where(property => property.ValueGenerated != ValueGenerated.Never)
            .Select(property => $"{property.DeclaringType.DisplayName()}.{property.Name}");

        generated.ShouldBeEmpty();
    }
}
