using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.UnitOfWork;

/// <summary>
/// Implements <see cref="IUnitOfWorkResolver"/> over keyed services, each module having registered
/// its own <see cref="IUnitOfWork"/> under the key of its command assembly.
/// </summary>
/// <param name="services">The container to look the module's registration up in.</param>
/// <remarks>
/// This type resolves a service by a key known only at run time, which is service location. It is
/// confined here on purpose, exactly as it is for message validation: the pipeline depends on
/// <see cref="IUnitOfWorkResolver"/> and never names a container, and this one adapter is the only
/// place that does.
/// </remarks>
public sealed class ModuleUnitOfWorkResolver(IServiceProvider services) : IUnitOfWorkResolver
{
    /// <inheritdoc/>
    public IUnitOfWork Resolve(ICommandBase command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var key = ModuleKey.For(command);
        var unitOfWork = services.GetKeyedService<IUnitOfWork>(key);

        if (unitOfWork is null)
        {
            throw new InvalidOperationException(
                $"No {nameof(IUnitOfWork)} is registered for the module that owns "
                + $"'{command.GetType().FullName}'. Its module must call AddModuleUnitOfWork with "
                + $"the assembly declaring its commands.");
        }

        return unitOfWork;
    }
}
