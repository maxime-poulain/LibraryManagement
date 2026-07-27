using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.CQS;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// The scan every rule in this project rests on, guarded on its own.
/// </summary>
/// <remarks>
/// This is the failure an architecture rule is least able to report. A rule that reaches nothing
/// passes, and passing is what a rule looks like when it is working — which is how a command handler
/// rule sat green beside the shared infrastructure tests, reaching three kernel assemblies and no
/// module at all, while the note it kept alongside recorded that no production handler existed yet.
/// Both statements were true of what the scan could see, and false of the solution.
/// </remarks>
public sealed class ProductionCodeTests
{
    [Fact]
    public void TheScan_ReachesEveryModuleAndNotOnlyTheKernel()
    {
        var names = ProductionCode.Assemblies().Select(assembly => assembly.GetName().Name).ToList();

        names.ShouldContain(typeof(Entity).Assembly.GetName().Name);
        names.ShouldContain(typeof(ICommandDispatcher).Assembly.GetName().Name);
        names.ShouldContain(typeof(MediatorCommandDispatcher).Assembly.GetName().Name);
        names.ShouldContain(typeof(Author).Assembly.GetName().Name);
        names.ShouldContain(typeof(RegisterAuthorCommandHandler).Assembly.GetName().Name);
        names.ShouldContain(typeof(CatalogDbContext).Assembly.GetName().Name);
    }

    [Fact]
    public void TheScan_LeavesOutTheAssemblyHoldingTheDeliberateViolations()
    {
        // Every fixture in this project breaks a rule on purpose. Were they scanned as production
        // code, each rule would report them and no real violation would ever be distinguishable.
        var names = ProductionCode.Assemblies().Select(assembly => assembly.GetName().Name).ToList();

        names.ShouldNotContain(ProductionCode.Fixtures.GetName().Name);
    }
}
