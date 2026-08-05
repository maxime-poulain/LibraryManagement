using System.Text.Json.Serialization;
using FluentValidation;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.Application.Copies.AcquireCopy;
using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Holdings.Infrastructure.Serialization;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Shared.Application.IntegrationEvents;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Holdings.Infrastructure.Extensions;

/// <summary>
/// Wires the Holdings module into a host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Holdings module owns: its store, its repository, its published
    /// language and its unit of work.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureStore">
    /// Chooses the database and the provider. The module maps to a table and a schema; which server
    /// holds them is the host's decision, and one it makes once.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <remarks>
    /// <para>
    /// One entry point per module, and the only public surface the infrastructure exposes to a host.
    /// </para>
    /// <para>
    /// <strong>A host that calls this must also register Catalog.</strong> Acquiring a copy asks
    /// Catalog's published language whether the edition exists, and nothing here supplies that
    /// answer. Holdings deliberately does not call <c>AddCatalogModule</c> for the host: a module
    /// that started another module would decide the composition, which is the host's business, and
    /// would quietly make the two impossible to deploy apart.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddHoldingsModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureStore)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStore);

        // Not AddDbContext. The shared registration attaches the interceptors every module's store
        // runs — the domain events on their way out, the audit stamps on their way in — so that is
        // not something a module has to remember.
        services.AddModuleStore<HoldingsDbContext>(configureStore);

        services.AddScoped<ICopyRepository, CopyRepository>();

        // The module's validators, from the assembly that declares its commands. The shared
        // validation behavior resolves them by the message's concrete type, so a validator nobody
        // registered is a validator that silently never runs.
        services.AddValidatorsFromAssemblyContaining<AcquireCopyCommandValidator>(
            ServiceLifetime.Scoped);

        // Keyed by the assembly declaring this module's commands. Catalog is keyed by its own, and
        // the pipeline now genuinely has to tell them apart.
        services.AddModuleUnitOfWork<HoldingsUnitOfWork>(
            typeof(AcquireCopyCommand).Assembly);

        // What this module publishes to the ones downstream of it — Circulation, when it exists.
        services.AddScoped<ICopyLendability, Infrastructure.PublishedLanguage.CopyLendability>();

        // And what it listens to. Registered unconditionally, even in a composition without
        // Circulation: a subscriber nobody announces to is never resolved, whereas one registered
        // only when Circulation happens to be present would make "does Holdings react?" depend on
        // the order two AddModule calls were written in.
        services.AddScoped<
            IIntegrationEventSubscriber<CopyReportedLost>,
            IntegrationEvents.DeclareCopyLostOnCopyReportedLost>();

        // The JSON side of this module's value objects, for the outbox.
        services.AddSingleton<JsonConverter, BarcodeJsonConverter>();
        services.AddSingleton<JsonConverter, ShelfmarkJsonConverter>();

        return services;
    }
}
