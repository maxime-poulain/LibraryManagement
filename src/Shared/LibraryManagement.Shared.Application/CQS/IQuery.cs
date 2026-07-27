using LibraryManagement.Shared.Domain.Results;
using Mediator;

namespace LibraryManagement.Shared.Application.CQS;

/// <summary>
/// Marker interface for queries.
/// </summary>
public interface IQuery : IMessage
{
}

/// <summary>
/// Represents a query answered by a <see cref="Result{TValue}"/>.
/// </summary>
/// <typeparam name="TValue">The type of the data carried by a successful answer.</typeparam>
/// <remarks>
/// <para>
/// The type argument names what a successful query carries, not what the handler returns: reading
/// can fail for reasons that are not exceptional — an identifier that matches nothing, a caller
/// without the rights to see the record — and those answers travel as errors rather than as a null
/// or an exception, exactly as they do on the command side. That a query answers with a
/// <see cref="Result{TValue}"/> is not a convention anyone has to follow: it is written into this
/// interface, and no query can be declared any other way.
/// </para>
/// <para>
/// <typeparamref name="TValue"/> is suffixed <c>Dto</c>, and an architecture test enforces it over
/// every module. The suffix marks a type that exists to be handed out: it may be shaped for its
/// consumer, and it owes nothing to the model it was read from. What the rule really catches is the
/// query that answers with a domain type — an aggregate or a value object — which compiles perfectly
/// well and exports the model of a bounded context to everyone who reads from it. Types outside the
/// solution are left alone, so a query may still answer with a <see cref="string"/> or a count.
/// </para>
/// </remarks>
public interface IQuery<TValue> : IQuery, IRequest<Result<TValue>>
{
}

/// <summary>
/// Represents the handler for a query answered by a <see cref="Result{TValue}"/>.
/// </summary>
/// <typeparam name="TQuery">The type of the query being handled.</typeparam>
/// <typeparam name="TValue">The type of the data carried by a successful answer.</typeparam>
public interface IQueryHandler<in TQuery, TValue> : IRequestHandler<TQuery, Result<TValue>>
    where TQuery : IQuery<TValue>
{
}
