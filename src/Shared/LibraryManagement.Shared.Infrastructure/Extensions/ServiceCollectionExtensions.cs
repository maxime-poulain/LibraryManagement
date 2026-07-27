using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Infrastructure.CQS;
using LibraryManagement.Shared.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Extensions;

/// <summary>
/// Registers the parts of the infrastructure that every module shares.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the command and query dispatchers, and the command validator.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <remarks>
    /// Deliberately registers nothing else. A module owns its own <c>DbContext</c>, its own
    /// repositories and its own <see cref="Application.ITransactionManager"/> implementation, and
    /// registers them itself — shared infrastructure that knew about a specific module would defeat
    /// the separation the modular monolith is built on.
    /// </remarks>
    public static IServiceCollection AddSharedInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddScoped<ICommandDispatcher, MediatorCommandDispatcher>()
            .AddScoped<IQueryDispatcher, MediatorQueryDispatcher>()
            .AddScoped<ICommandValidator, FluentValidationCommandValidator>();
    }
}
