using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LibraryManagement.Charges.Infrastructure.Tests.Persistence;

/// <summary>
/// What the mapping says, checked without a server.
/// </summary>
/// <remarks>
/// Building the model validates every configuration in the assembly and connects to nothing, so
/// these run in the pull-request build where the integration tests do not. That matters most for
/// this module: its charges are the one hierarchy in the solution, and a hierarchy is exactly the
/// kind of mapping that fails at model-building time rather than at the first query.
/// </remarks>
public sealed class ChargesModelTests
{
    private static IModel Model()
    {
        using var context = new ChargesDbContext(
            new DbContextOptionsBuilder<ChargesDbContext>()
                .UseSqlServer("Server=nowhere;Database=none;Trusted_Connection=True;")
                .Options);

        return context.Model;
    }

    [Fact]
    public void TheModel_Builds()
    {
        Should.NotThrow(Model);
    }

    [Fact]
    public void EveryTable_LivesInTheChargesSchema()
    {
        var strays = Model().GetEntityTypes()
            .Where(entity => entity.GetSchema() is not null and not ChargesDbContext.Schema)
            .Select(entity => entity.DisplayName());

        strays.ShouldBeEmpty();
    }

    [Fact]
    public void BothKindsOfCharge_ShareOneTable()
    {
        var model = Model();

        model.FindEntityType(typeof(OverdueFine))!.GetTableName()
            .ShouldBe(model.FindEntityType(typeof(ReplacementCharge))!.GetTableName());
    }

    [Fact]
    public void TheKind_IsStoredAsItsName()
    {
        // A human reads this table when something looks wrong, and 'ReplacementCharge' answers
        // where '1' asks.
        var discriminator = Model().FindEntityType(typeof(Charge))!.FindDiscriminatorProperty();

        discriminator.ShouldNotBeNull().ClrType.ShouldBe(typeof(string));
        Model().FindEntityType(typeof(ReplacementCharge))!.GetDiscriminatorValue()
            .ShouldBe(nameof(ReplacementCharge));
    }

    [Fact]
    public void NoKeyIsLeftForTheEngineToGenerate()
    {
        // The rule the owned-collection defect cost a release to learn: a key EF believes it
        // generates is a key EF believes already has a row, so an entry added to an aggregate the
        // store already holds is marked Modified rather than Added.
        //
        // Over the domain's own types only. The outbox row's identity is an identity column on
        // purpose — the queue needs a strict insertion order, and a version 7 identifier orders
        // only to the millisecond — so it is the one key here the engine is meant to issue.
        var generated = Model().GetEntityTypes()
            .Where(entity => entity.ClrType.Assembly == typeof(Charge).Assembly)
            .SelectMany(entity => entity.FindPrimaryKey()?.Properties ?? [])
            .Where(property => property.ValueGenerated != ValueGenerated.Never)
            .Select(property => $"{property.DeclaringType.DisplayName()}.{property.Name}");

        generated.ShouldBeEmpty();
    }

    [Fact]
    public void TheBalance_IsNotAColumn()
    {
        // Computed from the charges. A stored total would be a second source of one truth, and the
        // day it disagreed nothing would say which was right.
        Model().FindEntityType(typeof(MemberAccount))!
            .FindProperty(nameof(MemberAccount.Balance))
            .ShouldBeNull();
    }

    [Fact]
    public void AnAmount_KeepsTheHundredthsMoneyIsCountedIn()
    {
        var amount = Model().FindEntityType(typeof(Charge))!.FindProperty(nameof(Charge.Amount));

        amount.ShouldNotBeNull().GetScale().ShouldBe(2);
    }

    [Fact]
    public void ACharge_ReachesItsAccount()
    {
        // The aggregate is loaded through this navigation and never sideways, so its absence would
        // be a silent boundary hole rather than a compile error.
        Model().FindEntityType(typeof(MemberAccount))!
            .FindNavigation(nameof(MemberAccount.Charges))
            .ShouldNotBeNull();
    }

    [Fact]
    public void DomainEvents_AreNotPersisted()
    {
        Model().FindEntityType(typeof(MemberAccount))!
            .FindProperty(nameof(MemberAccount.DomainEvents))
            .ShouldBeNull();
    }
}
