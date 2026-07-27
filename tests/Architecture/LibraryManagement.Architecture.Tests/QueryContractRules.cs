using System.Collections;
using System.Reflection;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Architecture.Tests;

/// <summary>
/// A query answers with a type meant to be handed out, and its name says so: the answer's type is
/// suffixed <c>Dto</c>.
/// </summary>
/// <remarks>
/// <para>
/// The half of the contract that says a query answers with a <see cref="Result{TValue}"/> needs no
/// rule — <see cref="IQuery{TValue}"/> derives from <c>IRequest&lt;Result&lt;TValue&gt;&gt;</c>, so a
/// query returning anything else does not compile. This is the half a compiler cannot state.
/// </para>
/// <para>
/// What it catches is worth more than tidy names. A query answering with an aggregate or a value
/// object compiles perfectly well, and every consumer of that read then compiles against the module's
/// own model — the export a bounded context exists to prevent, arriving through the read side because
/// the read side is where nobody is watching for it. The suffix is the visible half of that rule, and
/// a type that cannot take the suffix honestly is exactly the type that must not be returned.
/// </para>
/// <para>
/// Types from outside the solution are left alone: a query may answer with a <see cref="string"/> or
/// a count, and neither has a name to carry a convention. Collections are unwrapped, so a query
/// answering with many rows is judged on the row.
/// </para>
/// <para>
/// Known limit: the rule reaches the answer's own type and not what that type holds. A
/// <c>WorkDetailsDto</c> exposing a domain <c>Title</c> in a property passes. Following the graph
/// would mean deciding how deep to go and what to do about cycles, for a mistake a compiler warning
/// about the reference would already have made obvious.
/// </para>
/// </remarks>
public static class QueryContractRules
{
    private const string RequiredSuffix = "Dto";

    /// <summary>
    /// Returns one readable sentence per violation found, and an empty list when the rule holds.
    /// </summary>
    public static IReadOnlyList<string> FindQueriesNotAnsweringWithADto(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return FindQueries(assemblies)
            .Select(query => new { Query = query, Answer = AnswerOf(query) })
            .Where(pair => pair.Answer is not null && !IsAcceptableAnswer(pair.Answer))
            .Select(pair => Describe(pair.Query, pair.Answer!))
            .ToList();
    }

    /// <summary>
    /// Returns every concrete query found, so a scan that covers nothing can be told apart from a
    /// scan that found nothing wrong.
    /// </summary>
    public static IReadOnlyList<Type> FindQueries(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsQuery)
            .ToList();
    }

    private static bool IsQuery(Type type)
        => type is { IsAbstract: false, IsInterface: false } && ClosedQueryInterface(type) is not null;

    private static Type? ClosedQueryInterface(Type type)
        => Array.Find(
            type.GetInterfaces(),
            contract => contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(IQuery<>));

    // The type a successful answer carries, unwrapped from whatever collection carries it. A query
    // answering with many rows is judged on the row: the convention is about what leaves the module,
    // and a list adds nothing to that.
    private static Type? AnswerOf(Type query)
    {
        var answer = ClosedQueryInterface(query)?.GetGenericArguments()[0];

        return answer is null ? null : ElementOf(answer);
    }

    private static Type ElementOf(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return type;
        }

        var enumerable = Array.Find(
            type.GetInterfaces(),
            contract => contract.IsGenericType
                        && contract.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable is null ? type : ElementOf(enumerable.GetGenericArguments()[0]);
    }

    // Only what this solution declares. A string, a count or a date has no name to carry a
    // convention, and inventing a wrapper for one would be ceremony rather than a boundary.
    private static bool IsAcceptableAnswer(Type answer)
        => !IsOurs(answer) || answer.Name.EndsWith(RequiredSuffix, StringComparison.Ordinal);

    private static bool IsOurs(Type type)
        => type.Assembly.GetName().Name?.StartsWith("LibraryManagement", StringComparison.Ordinal) == true;

    private static string Describe(Type query, Type answer)
        => $"{query.FullName} answers with {answer.Name}, which is not suffixed '{RequiredSuffix}'. "
           + "A query answers with a type meant to be handed out, shaped for its consumer and owing "
           + "nothing to the model it was read from. Returning a domain type here would export the "
           + "module's model to everyone who reads through it.";
}
