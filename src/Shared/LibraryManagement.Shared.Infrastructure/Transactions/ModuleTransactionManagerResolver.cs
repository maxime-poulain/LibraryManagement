using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Transactions;

/// <summary>
/// Implements <see cref="ITransactionManagerResolver"/> over keyed services, each module having
/// registered its own <see cref="ITransactionManager"/> under the key of its command assembly.
/// </summary>
/// <remarks>
/// This type resolves a service from the container by a key known only at run time, which is service
/// location. It is confined here on purpose, exactly as it is for command validation: the dispatcher
/// depends on <see cref="ITransactionManagerResolver"/> and never names a container, and this one
/// adapter is the only place that does.
/// </remarks>
public sealed class ModuleTransactionManagerResolver(IServiceProvider services)
    : ITransactionManagerResolver
{
    /// <inheritdoc/>
    public ITransactionManager Resolve(ICommandBase command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var key = ModuleKey.For(command);
        var transactionManager = services.GetKeyedService<ITransactionManager>(key);

        if (transactionManager is null)
        {
            throw new InvalidOperationException(
                $"No {nameof(ITransactionManager)} is registered for the module that owns "
                + $"'{command.GetType().FullName}'. Its module must call "
                + $"AddModuleTransactionManager with the assembly declaring its commands.");
        }

        return transactionManager;
    }
}
