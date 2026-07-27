using System.Reflection;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Infrastructure.Auditing;
using LibraryManagement.Shared.Infrastructure.Behaviors;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.DomainEvents;
using LibraryManagement.Shared.Infrastructure.UnitOfWork;
using LibraryManagement.Shared.Infrastructure.Validation;
using Mediator;
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
    /// Registers the dispatchers, the pipeline behaviors every message goes through, the message
    /// validator, and the resolver that finds a command's module unit of work.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>The order of the two behaviors is the guarantee.</strong> Validation is registered
    /// first, so it runs first: a command rejected for a missing field never reaches the store.
    /// Register them the other way round and everything still compiles, every other test still
    /// passes, and the only thing that changes is that malformed commands start writing. A test
    /// pins the order for exactly that reason.
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

        return services
            .AddScoped<ICommandDispatcher, MediatorCommandDispatcher>()
            .AddScoped<IQueryDispatcher, MediatorQueryDispatcher>()
            .AddScoped<IMessageValidator, FluentValidationMessageValidator>()
            .AddScoped<IUnitOfWorkResolver, ModuleUnitOfWorkResolver>()
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
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
    /// publish nothing, which is the kind of omission that surfaces months later as "the notification
    /// never went out".
    /// </para>
    /// <para>
    /// <strong>The order of the two interceptors is the guarantee.</strong> Domain events are
    /// published first, so what a handler changes is tracked before the audit interceptor walks the
    /// change tracker; register them the other way round and a handler's writes reach the store
    /// stamped with nothing.
    /// </para>
    /// <para>
    /// The <see cref="IServiceProvider"/> overload of <c>AddDbContext</c> is what makes this
    /// possible: the interceptors are scoped — one holds the publisher for the current scope, the
    /// other the staff member acting in it — and only that overload can reach a scope to resolve
    /// them. It also makes the options themselves scoped rather than singleton, which is required
    /// here rather than incidental: singleton options would capture one scope's interceptors and hand
    /// them to every request for the life of the process.
    /// </para>
    /// <para>
    /// The cost is one options object built per scope instead of one for the whole process, which is
    /// an allocation rather than a query. What it does not do is defeat EF Core's internal service
    /// provider cache: the shape of the options is identical every time and only the interceptor
    /// instances differ, so nothing forces a second provider to be built.
    /// </para>
    /// <para>
    /// Everything the interceptors need is registered with <c>TryAdd</c>, because five modules will
    /// each call this and only the first call should take effect. It is also what lets a host replace
    /// <see cref="ICurrentUser"/> with a real implementation without having to remove the placeholder
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

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICurrentUser, UnattributedUser>();
        services.TryAddScoped<IDomainEventPublisher, MediatorDomainEventPublisher>();
        services.TryAddScoped<DomainEventInterceptor>();
        services.TryAddScoped<AuditInterceptor>();

        services.AddDbContext<TContext>((serviceProvider, options) =>
        {
            configureStore(options);

            options.AddInterceptors(
                serviceProvider.GetRequiredService<DomainEventInterceptor>(),
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
