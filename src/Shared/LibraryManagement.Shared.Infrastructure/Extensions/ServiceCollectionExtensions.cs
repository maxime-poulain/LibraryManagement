using System.Reflection;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Transactions;
using LibraryManagement.Shared.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Extensions;

/// <summary>
/// Registers the parts of the infrastructure that every module shares.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the command and query dispatchers, the command validator, and the resolver that
    /// finds a command's module transaction manager.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <remarks>
    /// Deliberately registers nothing else. A module owns its own <c>DbContext</c>, its own
    /// repositories and its own <see cref="ITransactionManager"/> implementation, and registers them
    /// itself through <see cref="AddModuleTransactionManager{TTransactionManager}"/> — shared
    /// infrastructure that knew about a specific module would defeat the separation the modular
    /// monolith is built on.
    /// </remarks>
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddScoped<ICommandDispatcher, MediatorCommandDispatcher>()
            .AddScoped<IQueryDispatcher, MediatorQueryDispatcher>()
            .AddScoped<ICommandValidator, FluentValidationCommandValidator>()
            .AddScoped<ITransactionManagerResolver, ModuleTransactionManagerResolver>();
    }

    /// <summary>
    /// Registers a module's <see cref="ITransactionManager"/> under the module its commands belong
    /// to, so the command dispatcher can find it again from any command that module declares.
    /// </summary>
    /// <typeparam name="TTransactionManager">The module's transaction manager.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="commandAssembly">
    /// The assembly declaring the module's commands. Passing the module's own application assembly
    /// is what ties every command it declares to this transaction manager.
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
    public static IServiceCollection AddModuleTransactionManager<TTransactionManager>(
        this IServiceCollection services,
        Assembly commandAssembly)
        where TTransactionManager : class, ITransactionManager
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(commandAssembly);

        return services.AddKeyedScoped<ITransactionManager, TTransactionManager>(
            ModuleKey.For(commandAssembly));
    }
}
