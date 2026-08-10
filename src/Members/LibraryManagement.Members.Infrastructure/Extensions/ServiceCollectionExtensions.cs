using System.Text.Json.Serialization;
using FluentValidation;
using LibraryManagement.Members.Application.Members.EnrollMember;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Members.Infrastructure.Serialization;
using LibraryManagement.Members.PublishedLanguage;
using LibraryManagement.Shared.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Members.Infrastructure.Extensions;

/// <summary>
/// Wires the Members module into a host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything the Members module owns: its store, its repository, its published
    /// language and its unit of work.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configureStore">
    /// Chooses the database and the provider. The module maps to a table and a schema; which
    /// server holds them is the host's decision, and one it makes once.
    /// </param>
    /// <returns>The same collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <remarks>
    /// <para>
    /// One entry point per module, and the only public surface the infrastructure exposes to a
    /// host.
    /// </para>
    /// <para>
    /// A host that calls this needs to register nothing else for it: Members is upstream of
    /// everything it touches, and no command here asks another module a question. The contrast
    /// with Holdings — whose registration documents its dependence on Catalog — is the context
    /// map, visible in the composition root.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddMembersModule(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureStore)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureStore);

        // Not AddDbContext. The shared registration attaches the interceptors every module's store
        // runs — the domain events on their way out, the audit stamps on their way in — so that is
        // not something a module has to remember.
        services.AddModuleStore<MembersDbContext>(configureStore);

        services.AddScoped<IMemberRepository, MemberRepository>();

        // The rules of a merge, which belong to neither record alone and need no store once both
        // are loaded. Scoped like every other seam; the service is stateless, so the lifetime
        // carries nothing.
        services.AddScoped<IMemberMergeDomainService, MemberMergeDomainService>();

        // The module's validators, from the assembly that declares its commands. The shared
        // validation behavior resolves them by the message's concrete type, so a validator nobody
        // registered is a validator that silently never runs.
        services.AddValidatorsFromAssemblyContaining<EnrollMemberCommandValidator>(
            ServiceLifetime.Scoped);

        // Keyed by the assembly declaring this module's commands — the third registration of one
        // interface, which is exactly the arrangement the keyed resolver exists for.
        services.AddModuleUnitOfWork<MembersUnitOfWork>(
            typeof(EnrollMemberCommand).Assembly);

        // What this module publishes to the ones downstream of it — Circulation, when it exists.
        services.AddScoped<IMemberEntitlement, Infrastructure.PublishedLanguage.MemberEntitlement>();

        // The JSON side of this module's value objects, for the outbox.
        services.AddSingleton<JsonConverter, CardNumberJsonConverter>();
        services.AddSingleton<JsonConverter, MemberNameJsonConverter>();
        services.AddSingleton<JsonConverter, ContactDetailsJsonConverter>();
        services.AddSingleton<JsonConverter, GuardianJsonConverter>();

        return services;
    }
}
