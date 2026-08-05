using System.Text.Json.Serialization;
using FluentValidation;
using LibraryManagement.Charges.Application.Accounts.TakePayment;
using LibraryManagement.Charges.Domain;
using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Charges.Infrastructure.Serialization;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.IntegrationEvents;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Charges.Infrastructure.Extensions;

/// <summary>
/// Wires the Charges module into a host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Charges module owns: its store, its repository, its tariff, its unit
    /// of work, and the port Circulation declared for it.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureStore">
    /// Chooses the database and the provider. The module maps to tables and a schema; which server
    /// holds them is the host's decision, and one it makes once.
    /// </param>
    /// <param name="policy">
    /// The tariff. Defaults to what the library charges today; a host with the numbers in
    /// configuration passes its own, which is the whole reason they are a record rather than
    /// constants.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either required argument is null.</exception>
    /// <remarks>
    /// <para>
    /// One entry point per module, and the only public surface the infrastructure exposes to a host.
    /// </para>
    /// <para>
    /// <strong>Registering this replaces whatever was answering Circulation's balance question.</strong>
    /// A composition that registers both this and a stand-in will find the stand-in silently
    /// winning or losing by registration order, which is why a host that has Charges should have
    /// nothing else implementing that port.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddChargesModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureStore,
        ChargesPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStore);

        // Not AddDbContext. The shared registration attaches the interceptors every module's store
        // runs — the domain events on their way out, the audit stamps on their way in.
        services.AddModuleStore<ChargesDbContext>(configureStore);

        services.AddScoped<IMemberAccountRepository, MemberAccountRepository>();

        // The tariff, as data. A decision of the library must not be a deployment, and this is the
        // context whose numbers move most often.
        services.AddSingleton(policy ?? ChargesPolicy.Current);

        // The module's validators, from the assembly that declares its commands. The shared
        // validation behavior resolves them by the message's concrete type, so a validator nobody
        // registered is a validator that silently never runs.
        services.AddValidatorsFromAssemblyContaining<TakePaymentCommandValidator>(
            ServiceLifetime.Scoped);

        // Keyed by the assembly declaring this module's commands — which matters more here than
        // anywhere, because a Charges command is usually dispatched from inside another module's
        // drain.
        services.AddModuleUnitOfWork<ChargesUnitOfWork>(typeof(TakePaymentCommand).Assembly);

        // The port Circulation declared and this module answers.
        services.AddScoped<IMemberBalance, PublishedLanguage.MemberBalance>();

        // And the two facts it listens to. Registered unconditionally, even in a composition
        // without Circulation: a subscriber nobody announces to is never resolved, whereas one
        // registered only when Circulation happens to be present would make "does Charges react?"
        // depend on the order two AddModule calls were written in.
        services.AddScoped<
            IIntegrationEventSubscriber<LoanReturnedLate>,
            IntegrationEvents.AssessFineOnLoanReturnedLate>();

        services.AddScoped<
            IIntegrationEventSubscriber<LoanWrittenOff>,
            IntegrationEvents.RaiseChargeOnLoanWrittenOff>();

        // The JSON side of this module's value object, for the outbox.
        services.AddSingleton<JsonConverter, MoneyJsonConverter>();

        return services;
    }
}
