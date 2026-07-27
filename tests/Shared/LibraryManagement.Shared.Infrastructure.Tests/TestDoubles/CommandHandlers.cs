using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Infrastructure.Tests.TestDoubles;

// Fixtures for the architecture rule. They exist so the rule can be shown to catch a violation
// rather than merely to pass over an empty set of types. They live in the test assembly, which the
// production scan deliberately excludes.

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
