using System.Reflection;
using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Shared.Infrastructure.Tests.Architecture;

/// <summary>
/// A command handler orchestrates one use case: it reaches persistence through repositories and
/// announces what happened through domain events. It never dispatches another command, and never
/// reads through the query pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The rule is not a matter of taste. <c>MediatorCommandDispatcher</c> opens a transaction per
/// dispatch, so a nested command would try to begin a second transaction on a connection that
/// already has one — which the provider rejects. The rule is what keeps "one command, one
/// transaction, one unit of consistency" true.
/// </para>
/// <para>
/// Known limit: this inspects declared dependencies — constructor parameters and fields. A handler
/// that resolved a dispatcher from an <see cref="IServiceProvider"/> at call time would slip
/// through. That is a deliberate trade-off: catching it would need IL analysis, and service
/// location inside a handler is a smell a reviewer will see anyway.
/// </para>
/// </remarks>
public static class CommandHandlerRules
{
    private static readonly Type[] ForbiddenDependencies =
    [
        typeof(ICommandDispatcher),
        typeof(IQueryDispatcher),
    ];

    /// <summary>
    /// Returns one readable sentence per violation found, and an empty list when the rule holds.
    /// </summary>
    public static IReadOnlyList<string> FindHandlersDependingOnADispatcher(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsCommandHandler)
            .SelectMany(DescribeViolations)
            .ToList();
    }

    /// <summary>
    /// Returns every concrete command handler found, so a scan that covers nothing can be told
    /// apart from a scan that found nothing wrong.
    /// </summary>
    public static IReadOnlyList<Type> FindCommandHandlers(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsCommandHandler)
            .ToList();
    }

    private static bool IsCommandHandler(Type type)
        => type is { IsAbstract: false, IsInterface: false }
           && type.GetInterfaces().Any(contract =>
               contract.IsGenericType
               && contract.GetGenericTypeDefinition() == typeof(ICommandHandler<,>));

    private static IEnumerable<string> DescribeViolations(Type handler)
    {
        // Constructor parameters cover both classic and primary constructors; fields cover field
        // injection. A primary constructor shows up in both, hence the Distinct.
        var declaredDependencies = handler
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Concat(handler
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.FieldType));

        return declaredDependencies
            .Where(ForbiddenDependencies.Contains)
            .Distinct()
            .Select(dependency =>
                $"{handler.FullName} depends on {dependency.Name}. A command handler orchestrates "
                + "through repositories and raises domain events; it never dispatches another "
                + "command and never reads through the query pipeline.");
    }
}
