using FluentValidation;
using LibraryManagement.Circulation.Application.Loans.CheckOutCopy;
using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Circulation.Infrastructure.Extensions;

/// <summary>
/// Wires the Circulation module into a host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Circulation module owns: its store, its repositories, its policy
    /// and its unit of work.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureStore">
    /// Chooses the database and the provider. The module maps to tables and a schema; which
    /// server holds them is the host's decision, and one it makes once.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>A host that calls this must also register Holdings and Members</strong> — checking
    /// out asks both published languages — <strong>and supply an
    /// <c>IMemberBalance</c></strong>: the port Circulation declared for Charges to implement.
    /// Until Charges exists, the host stands in, and what it answers is the host's statement of
    /// what "no Charges module yet" means. Circulation registers none of the three itself: a
    /// module that started another module would decide the composition, which is the host's
    /// business.
    /// </para>
    /// <para>
    /// The policy is registered as the single instance the library has decided. The day its
    /// values come from a table, this is the one line that changes.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddCirculationModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureStore)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStore);

        // Not AddDbContext. The shared registration attaches the interceptors every module's store
        // runs — the domain events on their way out, the audit stamps on their way in.
        services.AddModuleStore<CirculationDbContext>(configureStore);

        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<IHoldQueueRepository, HoldQueueRepository>();

        services.AddSingleton(CirculationPolicy.Current);

        // The module's validators, from the assembly that declares its commands. A validator
        // nobody registered is a validator that silently never runs.
        services.AddValidatorsFromAssemblyContaining<CheckOutCopyCommandValidator>(
            ServiceLifetime.Scoped);

        // Keyed by the assembly declaring this module's commands, like every module's.
        services.AddModuleUnitOfWork<CirculationUnitOfWork>(
            typeof(CheckOutCopyCommand).Assembly);

        // No JSON converters: this module's events carry identifiers, dates and enums, all of
        // which the shared serializer already speaks. No published-language adapter either — the
        // one port this module declares is implemented on the other side of its edge.
        return services;
    }
}
