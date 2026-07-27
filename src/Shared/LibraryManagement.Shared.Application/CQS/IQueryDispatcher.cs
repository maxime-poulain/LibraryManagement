using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Application.CQS;

/// <summary>
/// Represents a query dispatcher.
/// </summary>
public interface IQueryDispatcher
{
    /// <summary>
    /// Dispatches a query and returns its answer.
    /// </summary>
    /// <typeparam name="TValue">The type of the data carried by a successful answer.</typeparam>
    /// <param name="query">Query to operate on</param>
    /// <param name="cancellationToken">A token to cancel the current asynchronous operation.</param>
    /// <returns>The answer to the query, carrying either the data or the errors.</returns>
    public ValueTask<Result<TValue>> DispatchAsync<TValue>(
        IQuery<TValue> query,
        CancellationToken cancellationToken = default);
}
