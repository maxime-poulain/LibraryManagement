using System.Reflection;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// A module subscribes to another module's <em>published language</em>, and to nothing else of it.
/// </summary>
/// <remarks>
/// <para>
/// This is the rule the cross-module passage stands on, and the one a compiler cannot state. A
/// subscriber names the contract it reacts to, and nothing stops that contract being the announcing
/// module's own domain event — it compiles, it runs, and the reacting module now holds a reference to
/// the other's <c>Domain</c>. Every rename on the announcing side becomes a change on the reacting
/// side, which is the coupling the whole arrangement exists to prevent.
/// </para>
/// <para>
/// A published-language assembly is recognised by its name rather than by a marker, deliberately:
/// those projects reference nothing at all, so there is no marker they could carry — the same
/// constraint that keeps the mechanism generic in the first place.
/// </para>
/// <para>
/// Known limit: the rule reaches the contract's own type and not what that type holds. A contract
/// exposing a domain type in a property would pass — but it could not, because a published-language
/// project references nothing and therefore has no domain type to expose. The constraint the compiler
/// does enforce is what makes this rule's shallowness safe.
/// </para>
/// </remarks>
public static class IntegrationContractRules
{
    private const string PublishedLanguageSuffix = ".PublishedLanguage";

    /// <summary>
    /// Returns one readable sentence per violation found, and an empty list when the rule holds.
    /// </summary>
    public static IReadOnlyList<string> FindSubscribersReachingPastAPublishedLanguage(
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return FindSubscribers(assemblies)
            .SelectMany(
                ContractsOf,
                (subscriber, contract) => new { Subscriber = subscriber, Contract = contract })
            .Where(pair => !IsPublishedLanguage(pair.Contract))
            .Select(pair => Describe(pair.Subscriber, pair.Contract))
            .ToList();
    }

    /// <summary>
    /// Returns every concrete subscriber found, so a scan that covers nothing can be told apart from
    /// a scan that found nothing wrong.
    /// </summary>
    public static IReadOnlyList<Type> FindSubscribers(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && ContractsOf(type).Count > 0)
            .ToList();
    }

    private static List<Type> ContractsOf(Type type)
        => type.GetInterfaces()
            .Where(contract => contract.IsGenericType
                               && contract.GetGenericTypeDefinition()
                                   == typeof(IIntegrationEventSubscriber<>))
            .Select(contract => contract.GetGenericArguments()[0])
            .ToList();

    // By assembly name, because a published-language project references nothing and so cannot carry
    // a marker to be recognised by.
    private static bool IsPublishedLanguage(Type contract)
        => contract.Assembly.GetName().Name?.EndsWith(
            PublishedLanguageSuffix, StringComparison.Ordinal) == true;

    private static string Describe(Type subscriber, Type contract)
        => $"{subscriber.FullName} subscribes to {contract.FullName}, which does not live in a "
           + "published language. A module reacts to another module's published contracts and to "
           + "nothing else of it: subscribing to a domain event would put the announcing module's "
           + "model in this one's compile-time dependencies, and every rename over there would "
           + "become a change over here.";
}
