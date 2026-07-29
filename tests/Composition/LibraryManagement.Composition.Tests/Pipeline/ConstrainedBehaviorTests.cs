using LibraryManagement.Catalog.Infrastructure.Extensions;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Mediator declares its own ICommand<T>, IQuery<T> and their handlers. Its types are named in full
// here rather than imported, so every unqualified CQS name in this file is the kernel's own.
using ISender = Mediator.ISender;

namespace LibraryManagement.Composition.Tests.Pipeline;

/// <summary>
/// The property the whole pipeline design rests on: a behavior constrained to commands wraps a
/// command and is skipped for a query, rather than failing to close or throwing.
/// </summary>
/// <remarks>
/// <para>
/// Validation applies to every message; the unit of work applies to commands alone. If the
/// constraint were not honoured, the unit of work would have to stay in a dispatcher and the
/// migration would be pointless. The generator honours it at compile time — the generated
/// registrations close a command-constrained behavior over no query — and the container honours
/// it for behaviors added through DI, which is the path the fixtures below take.
/// </para>
/// <para>
/// The fixtures record only themselves, but they no longer run alone: the assembly's own pipeline
/// — declared once in <see cref="CompositionRoot"/> — wraps every message here too, fixture
/// commands included. That is why the fixture container carries a unit of work for this very
/// assembly: the real pipeline saves for every command of the module that declared it.
/// </para>
/// </remarks>
public sealed class ConstrainedBehaviorTests
{
    private static readonly List<string> Seen = [];

    // The generator registers every handler it found, Catalog's included, so the module that owns
    // their dependencies has to be registered too. That is what a composition root is, and
    // assembling one here is the point of this project.
    private static ISender SenderWith(params Type[] behaviorsInOrder)
    {
        var services = CompositionRoot.Services()
            .AddCatalogModule(options => options.UseSqlServer("Server=unused"))
            .AddModuleUnitOfWork<NoOpUnitOfWork>(typeof(PingCommand).Assembly);

        foreach (var behavior in behaviorsInOrder)
        {
            services.AddTransient(typeof(Mediator.IPipelineBehavior<,>), behavior);
        }

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    // Nothing to save: the fixture commands change nothing, and the store their "module" would
    // write to does not exist.
    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ABehaviorConstrainedToCommands_WrapsCommandsAndSkipsQueries()
    {
        Seen.Clear();
        var sender = SenderWith(typeof(EveryMessageBehavior<,>), typeof(CommandOnlyBehavior<,>));

        await sender.Send(new PingCommand(), TestContext.Current.CancellationToken);
        await sender.Send(new PingQuery(), TestContext.Current.CancellationToken);

        Seen.ShouldBe(["every:PingCommand", "commandOnly:PingCommand", "every:PingQuery"]);
    }

    [Fact]
    public async Task Behaviors_RunInRegistrationOrder()
    {
        // The order is the guarantee, not the behaviour. Validation must run before the unit of
        // work, or a message rejected for a missing field would still reach the store — and nothing
        // else would report a wrong registration order.
        Seen.Clear();
        var sender = SenderWith(typeof(CommandOnlyBehavior<,>), typeof(EveryMessageBehavior<,>));

        await sender.Send(new PingCommand(), TestContext.Current.CancellationToken);

        Seen.ShouldBe(["commandOnly:PingCommand", "every:PingCommand"]);
    }

    // Aligned positionally with IPipelineBehavior<TMessage, TResponse>, which is what lets plain
    // open-generic registration close it. This is the shape ValidationBehavior takes.
    internal sealed class EveryMessageBehavior<TMessage, TResponse> : Mediator.IPipelineBehavior<TMessage, TResponse>
        where TMessage : Mediator.IMessage
    {
        public ValueTask<TResponse> Handle(
            TMessage message,
            Mediator.MessageHandlerDelegate<TMessage, TResponse> next,
            CancellationToken cancellationToken)
        {
            Seen.Add($"every:{message.GetType().Name}");
            return next(message, cancellationToken);
        }
    }

    // The same shape, one constraint tighter. This is the shape UnitOfWorkBehavior takes.
    internal sealed class CommandOnlyBehavior<TMessage, TResponse> : Mediator.IPipelineBehavior<TMessage, TResponse>
        where TMessage : ICommandBase
    {
        public ValueTask<TResponse> Handle(
            TMessage message,
            Mediator.MessageHandlerDelegate<TMessage, TResponse> next,
            CancellationToken cancellationToken)
        {
            Seen.Add($"commandOnly:{message.GetType().Name}");
            return next(message, cancellationToken);
        }
    }
}

// One handler each, so the generator sees a coherent set.
public sealed record PingCommand : ICommand<Result>;

public sealed record PingQuery : IQuery<string>;

public sealed class PingCommandHandler : ICommandHandler<PingCommand, Result>
{
    public ValueTask<Result> Handle(PingCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result.Success());
}

public sealed class PingQueryHandler : IQueryHandler<PingQuery, string>
{
    public ValueTask<Result<string>> Handle(PingQuery query, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result<string>.Success("pong"));
}
