using System.Reflection;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// An aggregate root is mapped through <see cref="AggregateRootConfiguration{TAggregate,TId}"/>, and
/// not by implementing <see cref="IEntityTypeConfiguration{TEntity}"/> directly.
/// </summary>
/// <remarks>
/// <para>
/// The base class supplies the key, the identifier conversion, the concurrency token and the
/// exclusion of the domain events. A module is perfectly free to write those four lines itself, and
/// that freedom is what this rule takes away — not because the lines are tedious, but because one of
/// them fails silently.
/// </para>
/// <para>
/// A configuration that omits <c>IsRowVersion</c> builds a model, produces a schema and passes every
/// test in the suite. What it also does is remove optimistic concurrency from that aggregate, so two
/// employees editing the same record stop colliding and start overwriting each other. Nothing
/// reports it, and the catalogue is simply wrong afterwards. That is the mistake this rule exists to
/// make impossible rather than unlikely.
/// </para>
/// </remarks>
public static class MappingRules
{
    /// <summary>
    /// Returns one readable sentence per violation found, and an empty list when the rule holds.
    /// </summary>
    public static IReadOnlyList<string> FindAggregateConfigurationsBypassingTheBase(
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return FindAggregateConfigurations(assemblies)
            .Where(configuration => !DerivesFromTheBase(configuration))
            .Select(Describe)
            .ToList();
    }

    /// <summary>
    /// Returns every concrete configuration of an aggregate root, so a scan that covers nothing can
    /// be told apart from a scan that found nothing wrong.
    /// </summary>
    public static IReadOnlyList<Type> FindAggregateConfigurations(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsAggregateConfiguration)
            .ToList();
    }

    private static bool IsAggregateConfiguration(Type type)
        => type is { IsAbstract: false, IsInterface: false } && MappedAggregate(type) is not null;

    // The entity a configuration is for, when that entity is an aggregate root. A configuration for
    // anything else — an owned value object, a projection — is none of this rule's business.
    private static Type? MappedAggregate(Type type)
        => Array.Find(
                type.GetInterfaces(),
                contract => contract.IsGenericType
                            && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
            ?.GetGenericArguments()[0] is { } entity && typeof(IAggregateRoot).IsAssignableFrom(entity)
            ? entity
            : null;

    private static bool DerivesFromTheBase(Type configuration)
    {
        for (var current = configuration.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType
                && current.GetGenericTypeDefinition() == typeof(AggregateRootConfiguration<,>))
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(Type configuration)
        => $"{configuration.FullName} maps {MappedAggregate(configuration)!.Name} without deriving "
           + $"from {nameof(AggregateRootConfiguration<,>)}. An aggregate root is mapped through it, "
           + "which supplies the key, the identifier conversion, the rowversion and the exclusion of "
           + "the domain events. Written by hand, a missing rowversion builds, migrates and passes "
           + "every test, and only stops two employees from colliding over the same record.";
}
