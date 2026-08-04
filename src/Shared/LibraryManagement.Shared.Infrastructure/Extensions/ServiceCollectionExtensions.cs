using System.Reflection;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Infrastructure.Auditing;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.DomainEvents;
using LibraryManagement.Shared.Infrastructure.Outbox;
using LibraryManagement.Shared.Infrastructure.UnitOfWork;
using LibraryManagement.Shared.Infrastructure.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LibraryManagement.Shared.Infrastructure.Extensions;

/// <summary>
/// Registers the parts of the infrastructure that every module shares.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the dispatchers, the message validator, the resolver that finds a command's
    /// module unit of work, and everything else the pipeline behaviors depend on.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>The pipeline itself is not registered here.</strong> The behaviors are declared to
    /// <c>AddMediator</c> (<c>options.PipelineBehaviors</c>), inline at the composition root,
    /// because that is the mediator's own surface for them: the source generator parses that very
    /// syntax and emits one closed registration per message and behavior, honoring each
    /// behavior's constraints. An open generic added here would run beside the generated
    /// registrations, and every behavior would execute twice per message. The order the root must
    /// declare — logging, validation, unit of work — is recorded on
    /// <see cref="Behaviors.LoggingBehavior{TMessage, TResponse}"/> with its reasons, and the
    /// composition tests pin it from the outside.
    /// </para>
    /// <para>
    /// Deliberately registers nothing module-specific. A module owns its own <c>DbContext</c>, its
    /// repositories and its <see cref="IUnitOfWork"/>, and registers them itself through
    /// <see cref="AddModuleUnitOfWork{TUnitOfWork}"/>.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Logging itself, so a logger always resolves. No provider is added here: which sinks
        // exist — console, a collector, none at all — is the host's decision, and AddLogging
        // composes with whatever it chooses.
        services.AddLogging();

        return services
            .AddScoped<ICommandDispatcher, MediatorCommandDispatcher>()
            .AddScoped<IQueryDispatcher, MediatorQueryDispatcher>()
            .AddScoped<IMessageValidator, FluentValidationMessageValidator>()
            .AddScoped<IUnitOfWorkResolver, ModuleUnitOfWorkResolver>();
    }

    /// <summary>
    /// Registers a module's store, with the interceptors every module's store runs.
    /// </summary>
    /// <typeparam name="TContext">The module's context.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureStore">
    /// Chooses the database and the provider. Applied first, so a module or a host can still say
    /// anything it likes about the connection.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <remarks>
    /// <para>
    /// A module calls this instead of <c>AddDbContext</c> so that attaching the interceptors is not
    /// something a module can forget. A module that forgot would save perfectly well and silently
    /// record no events at all, which is the kind of omission that surfaces months later as "the
    /// notification never went out".
    /// </para>
    /// <para>
    /// Two interceptors ride every save: <see cref="OutboxInterceptor"/> turns the events the
    /// aggregates raised into outbox rows in the same save, and <see cref="AuditInterceptor"/>
    /// stamps the rows being written. Their order no longer carries a guarantee — nothing runs
    /// during the save anymore, so nothing can add trackable work between them — but it stays fixed
    /// because a deterministic pipeline is easier to reason about than an accidental one.
    /// </para>
    /// <para>
    /// The drain side is registered here too: the module's <see cref="OutboxProcessor{TContext}"/>,
    /// and the <see cref="IDomainEventPublisher"/> it delivers through. Nothing schedules the
    /// processor — that is the host's decision, exactly as the database provider is, which is also
    /// why no scheduler is named anywhere in a module.
    /// </para>
    /// <para>
    /// The <see cref="IServiceProvider"/> overload of <c>AddDbContext</c> is what lets the
    /// interceptors come from the container: the audit one is scoped — it holds the employee
    /// acting — so the options are scoped too rather than singleton, which is required rather than
    /// incidental: singleton options would capture one scope's interceptor and hand it to every
    /// request for the life of the process. The cost is one options object per scope, an allocation
    /// rather than a query, and EF Core's internal provider cache is unaffected because the shape of
    /// the options never changes.
    /// </para>
    /// <para>
    /// Everything shared is registered with <c>TryAdd</c>, because five modules will each call this
    /// and only the first call should take effect. It is also what lets a host replace
    /// <see cref="ICurrentEmployee"/> with a real implementation without having to remove the placeholder
    /// first.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddModuleStore<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureStore)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStore);

        // Idempotent, like everything below: the processor registered here logs, and a container
        // wiring a module without the shared pipeline must still resolve that logger.
        services.AddLogging();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICurrentEmployee, UnattributedEmployee>();
        services.TryAddSingleton<IDomainEventSerializer, JsonDomainEventSerializer>();
        services.TryAddSingleton<OutboxInterceptor>();
        services.TryAddScoped<AuditInterceptor>();

        // The drain: the processor for this module's table, and the delivery port it publishes
        // through, resolved per message from the scope the processor opens.
        services.TryAddScoped<IDomainEventPublisher, MediatorDomainEventPublisher>();
        services.TryAddSingleton<OutboxProcessor<TContext>>();

        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            configureStore(options);

            options.AddInterceptors(
                serviceProvider.GetRequiredService<OutboxInterceptor>(),
                serviceProvider.GetRequiredService<AuditInterceptor>());
        });

        return services;
    }

    /// <summary>
    /// Registers a module's <see cref="IUnitOfWork"/> under the module its commands belong to, so
    /// the pipeline can find it again from any command that module declares.
    /// </summary>
    /// <typeparam name="TUnitOfWork">The module's unit of work.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="commandAssembly">
    /// The assembly declaring the module's commands. Passing the module's own application assembly
    /// is what ties every command it declares to this unit of work.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="commandAssembly"/> is null.
    /// </exception>
    /// <remarks>
    /// Registered as keyed rather than plain, because several modules register an implementation of
    /// the same interface and a plain registration would let the last one silently answer for all of
    /// them.
    /// </remarks>
    public static IServiceCollection AddModuleUnitOfWork<TUnitOfWork>(
        this IServiceCollection services,
        Assembly commandAssembly)
        where TUnitOfWork : class, IUnitOfWork
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(commandAssembly);

        return services.AddKeyedScoped<IUnitOfWork, TUnitOfWork>(ModuleKey.For(commandAssembly));
    }
}
