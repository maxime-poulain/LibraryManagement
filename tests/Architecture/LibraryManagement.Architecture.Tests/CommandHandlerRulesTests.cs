using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Application.Works.RegisterWork;
using LibraryManagement.Holdings.Application.Copies.AcquireCopy;
using LibraryManagement.Members.Application.Members.EnrollMember;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// The rule, applied to every module and exercised against deliberate violations.
/// </summary>
public sealed class CommandHandlerRulesTests
{
    // --- The rule, applied to production code ----------------------------------------------------

    [Fact]
    public void NoCommandHandler_DependsOnADispatcher()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(ProductionCode.Assemblies());

        violations.ShouldBeEmpty();
    }

    [Fact]
    public void TheScan_FindsTheHandlersTheModulesDeclare()
    {
        // Reaching an assembly is not the same as recognizing what is in it, and this names the two
        // handlers that exist rather than counting them: a rule that quietly stopped recognizing
        // ICommandHandler would still see every assembly and still report no violation.
        //
        // Compared by name rather than by type, for the reason ProductionCode gives.
        var handlers = CommandHandlerRules
            .FindCommandHandlers(ProductionCode.Assemblies())
            .Select(handler => handler.FullName)
            .ToList();

        handlers.ShouldContain(typeof(RegisterAuthorCommandHandler).FullName);
        handlers.ShouldContain(typeof(RegisterWorkCommandHandler).FullName);

        // One from each module, so that a module added to the solution but forgotten in this
        // project's references is a failing test rather than a silently narrower scan. Green over
        // one module reads exactly like green over two.
        handlers.ShouldContain(typeof(AcquireCopyCommandHandler).FullName);
        handlers.ShouldContain(typeof(EnrollMemberCommandHandler).FullName);
    }

    // --- The rule itself, exercised against deliberate violations --------------------------------

    [Fact]
    public void TheRule_CatchesAHandlerInjectingTheCommandDispatcher()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(ProductionCode.Fixtures);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(HandlerInjectingTheCommandDispatcher), StringComparison.Ordinal)
            && violation.Contains(nameof(ICommandDispatcher), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_CatchesAHandlerInjectingTheQueryDispatcher()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(ProductionCode.Fixtures);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(HandlerInjectingTheQueryDispatcher), StringComparison.Ordinal)
            && violation.Contains(nameof(IQueryDispatcher), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_CatchesAClassicConstructorAndFieldInjection()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(ProductionCode.Fixtures);

        violations.ShouldContain(violation =>
            violation.Contains(nameof(HandlerHoldingADispatcherField), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_ReportsAViolationOnceEvenWhenItAppearsAsBothParameterAndField()
    {
        // A primary constructor produces a parameter and a backing field for the same dependency.
        var violations = CommandHandlerRules
            .FindHandlersDependingOnADispatcher(ProductionCode.Fixtures)
            .Where(violation =>
                violation.Contains(nameof(HandlerInjectingTheCommandDispatcher), StringComparison.Ordinal));

        violations.ShouldHaveSingleItem();
    }

    [Fact]
    public void TheRule_LeavesACompliantHandlerAlone()
    {
        var violations = CommandHandlerRules.FindHandlersDependingOnADispatcher(ProductionCode.Fixtures);

        violations.ShouldNotContain(violation =>
            violation.Contains(nameof(CompliantCommandHandler), StringComparison.Ordinal));
    }

    [Fact]
    public void TheRule_ExplainsWhyRatherThanJustNamingTheType()
    {
        // A failing architecture test is read by whoever broke it. The message is the documentation.
        var violation = CommandHandlerRules.FindHandlersDependingOnADispatcher(ProductionCode.Fixtures)[0];

        violation.ShouldContain("repositories");
        violation.ShouldContain("domain events");
    }

    [Fact]
    public void TheRule_FindsTheHandlersItIsSupposedToInspect()
    {
        var handlers = CommandHandlerRules.FindCommandHandlers(ProductionCode.Fixtures);

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

// Deliberate violations, and their compliant counterpart. They exist so the rule can be shown to
// catch something rather than merely to pass over an empty set of types. Four handlers share one
// command, which no container would accept and none is ever asked to: nothing here is dispatched,
// registered or run, and the rule reads these types rather than executing them.
public sealed record TestCommand : ICommand<Result>;

public sealed record TestQuery(string Term) : IQuery<string>;

public interface IFakeRepository
{
    ValueTask SaveAsync(CancellationToken cancellationToken);
}

// What a command handler is supposed to look like: it reaches persistence through a repository.
public sealed class CompliantCommandHandler(IFakeRepository repository)
    : ICommandHandler<TestCommand, Result>
{
    public async ValueTask<Result> Handle(TestCommand command, CancellationToken cancellationToken)
    {
        await repository.SaveAsync(cancellationToken);
        return Result.Success();
    }
}

// A command handler that dispatches another command.
public sealed class HandlerInjectingTheCommandDispatcher(ICommandDispatcher dispatcher)
    : ICommandHandler<TestCommand, Result>
{
    public ValueTask<Result> Handle(TestCommand command, CancellationToken cancellationToken)
        => dispatcher.DispatchAsync(command, cancellationToken);
}

// A command handler that reads through the query pipeline.
public sealed class HandlerInjectingTheQueryDispatcher(IQueryDispatcher dispatcher)
    : ICommandHandler<TestCommand, Result>
{
    public async ValueTask<Result> Handle(TestCommand command, CancellationToken cancellationToken)
    {
        await dispatcher.DispatchAsync(new TestQuery("anything"), cancellationToken);
        return Result.Success();
    }
}

// The same violation reached through a classic constructor and a field rather than a primary
// constructor, so the rule is shown to catch both shapes.
public sealed class HandlerHoldingADispatcherField : ICommandHandler<TestCommand, Result>
{
    private readonly ICommandDispatcher _dispatcher;

    public HandlerHoldingADispatcherField(ICommandDispatcher dispatcher) => _dispatcher = dispatcher;

    public ValueTask<Result> Handle(TestCommand command, CancellationToken cancellationToken)
        => _dispatcher.DispatchAsync(command, cancellationToken);
}
