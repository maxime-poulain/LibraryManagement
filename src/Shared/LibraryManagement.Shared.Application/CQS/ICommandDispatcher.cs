using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Shared.Application.CQS;

/// <summary>
/// Dispatches a command and reports whether it succeeded.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches a command asynchronously.
    /// </summary>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Success, or a failure carrying the errors that prevented the command from running.</returns>
    /// <remarks>
    /// Deliberately not generic over the result type. <see cref="ICommand{TResult}"/> is covariant
    /// and constrained to <see cref="Result"/>, of which <see cref="Result"/> itself is the only
    /// inhabitant — <c>Result&lt;TValue&gt;</c> does not derive from it, which is what keeps a
    /// command from returning a value. Naming the type here rather than leaving it open lets this
    /// method build a failure directly, which is what a validation error and a concurrency conflict
    /// both need to do.
    /// </remarks>
    public ValueTask<Result> DispatchAsync(
        ICommand<Result> command,
        CancellationToken cancellationToken = default);
}
