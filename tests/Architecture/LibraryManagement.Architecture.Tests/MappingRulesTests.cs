using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Persistence.Configurations;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// The rule, applied to every module and exercised against deliberate violations.
/// </summary>
public sealed class MappingRulesTests
{
    // --- The rule, applied to production code ----------------------------------------------------

    [Fact]
    public void EveryAggregateConfiguration_DerivesFromTheSharedBase()
    {
        var violations = MappingRules.FindAggregateConfigurationsBypassingTheBase(
            ProductionCode.Assemblies());

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void TheScan_FindsTheConfigurationsTheModulesDeclare()
    {
        // Compared by name rather than by type, for the reason ProductionCode gives.
        var configurations = MappingRules
            .FindAggregateConfigurations(ProductionCode.Assemblies())
            .Select(configuration => configuration.FullName)
            .ToList();

        configurations.ShouldContain(typeof(AuthorConfiguration).FullName);
        configurations.ShouldContain(typeof(WorkConfiguration).FullName);
    }

    // --- The rule itself, exercised against deliberate violations --------------------------------

    [Fact]
    public void TheRule_CatchesAConfigurationWrittenByHand()
    {
        var violations = MappingRules.FindAggregateConfigurationsBypassingTheBase(
            ProductionCode.Fixtures);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(HandWrittenAuthorConfiguration), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_LeavesAConfigurationUsingTheBaseAlone()
    {
        var violations = MappingRules.FindAggregateConfigurationsBypassingTheBase(
            ProductionCode.Fixtures);

        violations.ShouldNotContain(violation =>
            violation.Contains(nameof(ProperWorkConfiguration), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_LeavesAConfigurationForSomethingOtherThanAnAggregateAlone()
    {
        // An owned value object is configured by implementing the interface directly, and nothing
        // about the base class applies to it: it has no key of its own, no concurrency token and no
        // events to exclude.
        var violations = MappingRules.FindAggregateConfigurationsBypassingTheBase(
            ProductionCode.Fixtures);

        violations.ShouldNotContain(violation =>
            violation.Contains(nameof(PersonNameConfiguration), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_ExplainsWhyRatherThanJustNamingTheType()
    {
        // A failing architecture test is read by whoever broke it. The message is the documentation.
        var violation = MappingRules.FindAggregateConfigurationsBypassingTheBase(
            ProductionCode.Fixtures)[0];

        violation.ShouldContain("rowversion");
        violation.ShouldContain("colliding");
    }

    [Fact]
    public void TheRule_FindsTheConfigurationsItIsSupposedToInspect()
    {
        var configurations = MappingRules.FindAggregateConfigurations(ProductionCode.Fixtures);

        configurations.ShouldContain(typeof(HandWrittenAuthorConfiguration));
        configurations.ShouldContain(typeof(ProperWorkConfiguration));
    }

    [Fact]
    public void TheRule_WithNullAssemblies_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => MappingRules.FindAggregateConfigurationsBypassingTheBase(null!));
    }
}

// Deliberate violations, and their compliant counterparts. None is ever applied to a model: the rule
// reads the shape of these types rather than the mapping they would produce.
public sealed class HandWrittenAuthorConfiguration : IEntityTypeConfiguration<Author>
{
    public void Configure(EntityTypeBuilder<Author> builder)
    {
        // Exactly what the base class would have done, minus the one line whose absence is silent.
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(author => author.Id);
        builder.Ignore(author => author.DomainEvents);
    }
}

public sealed class ProperWorkConfiguration : AggregateRootConfiguration<Work, WorkId>
{
    protected override void ConfigureAggregate(EntityTypeBuilder<Work> builder)
    {
        // Nothing to add: what is being tested is the derivation, not the mapping.
    }
}

public sealed class PersonNameConfiguration : IEntityTypeConfiguration<PersonName>
{
    public void Configure(EntityTypeBuilder<PersonName> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Property(name => name.Value);
    }
}
