using FluentValidation;
using LibraryManagement.Catalog.Application.Authors.RegisterAuthor;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Catalog.Infrastructure.Extensions;

/// <summary>
/// Wires the Catalog module into a host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Catalog module owns: its store, its repositories, its read side and
    /// its unit of work.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureStore">
    /// Chooses the database and the provider. The module maps to tables and a schema; which server
    /// holds them is the host's decision, and one it makes once.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <remarks>
    /// One entry point per module, and it is the only public surface the infrastructure exposes to a
    /// host. A host that had to know about <see cref="CatalogDbContext"/> or <c>AuthorRepository</c>
    /// to start the module would be free to reach for them from anywhere else too.
    /// </remarks>
    public static IServiceCollection AddCatalogModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureStore)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStore);

        // Not AddDbContext. A module's store runs the interceptors every module's store runs — the
        // domain events on their way out, the audit stamps on their way in — and going through the
        // shared registration is what keeps that from being something a module has to remember.
        services.AddModuleStore<CatalogDbContext>(configureStore);

        // The write side only. Query handlers take the DbContext directly and are found by the
        // mediator, so the read side has nothing to register here.
        services.AddScoped<IAuthorRepository, AuthorRepository>();
        services.AddScoped<IWorkRepository, WorkRepository>();

        // The module's validators, from the assembly that declares its commands and queries. The
        // shared validation behavior resolves them by the message's concrete type, so a validator
        // nobody registered is a validator that silently never runs.
        services.AddValidatorsFromAssemblyContaining<RegisterAuthorCommandValidator>(
            ServiceLifetime.Scoped);

        // Keyed by the assembly declaring this module's commands. That is what lets the one shared
        // pipeline write a Catalog command's changes through the Catalog store, while five
        // other modules have each registered a unit of work of their own.
        services.AddModuleUnitOfWork<CatalogUnitOfWork>(
            typeof(RegisterAuthorCommand).Assembly);

        return services;
    }
}
