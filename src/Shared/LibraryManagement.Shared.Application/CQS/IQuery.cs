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
/// The type argument names what a successful query carries, not what the handler returns: reading
/// can fail for reasons that are not exceptional — an identifier that matches nothing, a caller
/// without the rights to see the record — and those answers travel as errors rather than as a null
/// or an exception, exactly as they do on the command side.
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
