using System.Reflection;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

namespace LibraryManagement.Shared.Infrastructure.Tests.Architecture;

public sealed class CommandHandlerRulesTests
{
    // Every LibraryManagement assembly sitting next to this one, test assemblies excluded. Modules
    // are picked up automatically as soon as this project references them, so nobody has to
    // remember to extend a list — forgetting would leave a module silently unprotected.
    private static Assembly[] ProductionAssemblies()
    {
        var directory = Path.GetDirectoryName(typeof(CommandHandlerRulesTests).Assembly.Location)!;

        return Directory
            .EnumerateFiles(directory, "LibraryManagement.*.dll")
            .Where(path => !Path.GetFileName(path).Contains(".Tests.", StringComparison.Ordinal))
            .Select(Assembly.LoadFrom)
            .ToArray();
    }

    private static Assembly TestAssembly => typeof(CommandHandlerRulesTests).Assembly;

    // --- The rule, applied to production code ----------------------------------------------------

    [Fact]
    public void NoCommandHandler_DependsOnADispatcher()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(ProductionAssemblies());

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void TheScan_CoversTheWholeKernel()
    {
        // Guards the discovery itself: a green rule over zero assemblies would protect nothing.
        var names = ProductionAssemblies().Select(assembly => assembly.GetName().Name).ToList();

        names.ShouldContain(typeof(Entity).Assembly.GetName().Name);
        names.ShouldContain(typeof(ICommandDispatcher).Assembly.GetName().Name);
        names.ShouldContain(typeof(MediatorCommandDispatcher).Assembly.GetName().Name);
    }

    [Fact]
    public void NoProductionCommandHandlerExistsYet()
    {
        // Recorded on purpose. Until a bounded context ships a handler, the rule above passes over
        // an empty set: it is the self-tests below that prove it bites. Delete this test as soon as
        // the first module lands — its failure is the signal that real coverage has begun.
        CommandHandlerRules.FindCommandHandlers(ProductionAssemblies()).ShouldBeEmpty();
    }

    // --- The rule itself, exercised against deliberate violations --------------------------------

    [Fact]
    public void TheRule_CatchesAHandlerInjectingTheCommandDispatcher()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(TestAssembly);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(HandlerInjectingTheCommandDispatcher), StringComparison.Ordinal)
            && violation.Contains(nameof(ICommandDispatcher), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_CatchesAHandlerInjectingTheQueryDispatcher()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(TestAssembly);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(HandlerInjectingTheQueryDispatcher), StringComparison.Ordinal)
            && violation.Contains(nameof(IQueryDispatcher), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_CatchesAClassicConstructorAndFieldInjection()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(TestAssembly);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(HandlerHoldingADispatcherField), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_ReportsAViolationOnceEvenWhenItAppearsAsBothParameterAndField()
    {
        // A primary constructor produces a parameter and a backing field for the same dependency.
        var violations = CommandHandlerRules
            .FindHandlersDependingOnADispatcher(TestAssembly)
            .Where(violation =>
                violation.Contains(nameof(HandlerInjectingTheCommandDispatcher), StringComparison.Ordinal));

        violations.ShouldHaveSingleItem();
    }

    [Fact]
    public void TheRule_LeavesACompliantHandlerAlone()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(TestAssembly);

        violations.ShouldNotContain(violation =>
            violation.Contains(nameof(CompliantCommandHandler), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_ExplainsWhyRatherThanJustNamingTheType()
    {
        // A failing architecture test is read by whoever broke it. The message is the documentation.
        var violation = CommandHandlerRules.FindHandlersDependingOnADispatcher(TestAssembly)[0];

        violation.ShouldContain("repositories");
        violation.ShouldContain("domain events");
    }

    [Fact]
    public void TheRule_FindsTheHandlersItIsSupposedToInspect()
    {
        var handlers = CommandHandlerRules.FindCommandHandlers(TestAssembly);

        handlers.ShouldContain(typeof(CompliantCommandHandler));
        handlers.ShouldContain(typeof(HandlerInjectingTheCommandDispatcher));
    }

    [Fact]
    public void TheRule_WithNullAssemblies_Throws()
    {
        Should.Throw<ArgumentNullException>(
            () => CommandHandlerRules.FindHandlersDependingOnADispatcher(null!));
    }
}
