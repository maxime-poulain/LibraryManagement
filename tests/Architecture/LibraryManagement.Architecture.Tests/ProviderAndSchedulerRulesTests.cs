using System.Reflection;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// No module names a database engine or a scheduler.
/// </summary>
/// <remarks>
/// <para>
/// The oldest written rule in the repository and, until the host existed, the least enforced: it
/// was held by nothing but the discipline of not adding a package reference. That was survivable
/// while <c>src/</c> held only modules and any provider reference would have looked obviously wrong.
/// It stopped being survivable the day <c>src/Host/</c> arrived carrying both on purpose — because
/// now there is a place in the same folder where the reference is correct, and a copied line is a
/// plausible accident rather than an obvious one.
/// </para>
/// <para>
/// What the rule protects is not tidiness. A module that named SQL Server would compile, pass every
/// test, and quietly make the choice of engine a thing five modules each get a vote on; a module
/// that named Hangfire would decide for the host when its own work runs. Both are decisions
/// [ADR-0013](../../../docs/adr/0013-composition-root-in-the-tests.md) and
/// [ADR-0015](../../../docs/adr/0015-the-first-host.md) put outside a module on purpose.
/// </para>
/// </remarks>
public sealed class ProviderAndSchedulerRulesTests
{
    // Matched against the simple name of every assembly a module references. Relational is the
    // layer a module may name; a provider is what it may not.
    private static readonly string[] ForbiddenReferences =
    [
        "Microsoft.EntityFrameworkCore.SqlServer",
        "Microsoft.Data.SqlClient",
        "Hangfire.Core",
        "Hangfire.SqlServer",
        "Hangfire.NetCore",
        "Hangfire.AspNetCore",
    ];

    // The host and the migrations projects name both on purpose, and are not modules. They are
    // excluded by name rather than by absence, so the rule stays right if a future change puts them
    // in the scan.
    private static bool IsAModule(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? string.Empty;

        return !name.EndsWith(".Host", StringComparison.Ordinal)
            && !name.EndsWith(".Migrations.SqlServer", StringComparison.Ordinal);
    }

    private static Assembly[] Modules()
        => [.. ProductionCode.Assemblies().Where(IsAModule)];

    [Fact]
    public void NoModule_NamesAProviderOrAScheduler()
    {
        var offenders = Modules()
            .SelectMany(
                assembly => assembly.GetReferencedAssemblies(),
                (assembly, referenced) => (Module: assembly.GetName().Name, Referenced: referenced.Name))
            .Where(pair => ForbiddenReferences.Contains(pair.Referenced, StringComparer.Ordinal))
            .Select(pair => $"{pair.Module} references {pair.Referenced}")
            .ToList();

        offenders.ShouldBeEmpty(
            "a module describes tables and business moments; which engine serves them and what puts "
            + "its work on a clock are the host's decisions");
    }

    [Fact]
    public void TheScan_SawEveryModulesInfrastructure()
    {
        // Green over nothing reads exactly like green over everything. Infrastructure is where a
        // provider reference would land, so the rule above is worthless if these are missing.
        var scanned = Modules().Select(assembly => assembly.GetName().Name).ToList();

        foreach (var module in new[] { "Catalog", "Charges", "Circulation", "Holdings", "Members" })
        {
            scanned.ShouldContain($"LibraryManagement.{module}.Infrastructure");
        }
    }

    [Fact]
    public void EveryModulesInfrastructure_NamesTheRelationalLayerItDoesMap()
    {
        // The other half of the same statement: a module may not name an engine, and does name the
        // relational layer — so the rule above is a boundary rather than a ban on persistence.
        var infrastructure = Modules()
            .Where(assembly => assembly.GetName().Name?.EndsWith(".Infrastructure", StringComparison.Ordinal) == true)
            .Where(assembly => assembly.GetName().Name?.StartsWith("LibraryManagement.Shared", StringComparison.Ordinal) != true);

        foreach (var assembly in infrastructure)
        {
            assembly.GetReferencedAssemblies()
                .Select(referenced => referenced.Name)
                .ShouldContain(
                    "Microsoft.EntityFrameworkCore.Relational",
                    $"{assembly.GetName().Name} maps tables and schemas");
        }
    }
}
