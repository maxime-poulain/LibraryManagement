namespace LibraryManagement.Shared.Application;

/// <summary>
/// Opens the transaction that delimits a single command. Implemented by each module over its own
/// persistence store; the shared kernel only declares the contract.
/// </summary>
/// <remarks>
/// A command is the unit of consistency: either everything it changed is persisted, or none of it
/// is. This abstraction is what lets <c>ICommandDispatcher</c> enforce that without the shared
/// kernel knowing anything about a database.
/// </remarks>
public interface ITransactionManager
{
    /// <summary>
    /// Begins a transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The transaction. Disposing it without having committed rolls it back, so a caller that
    /// abandons the operation — by returning a failure, or by throwing — never leaves changes
    /// half-applied.
    /// </returns>
    ValueTask<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A transaction in progress. Commit it to keep the work, or dispose it to discard the work.
/// </summary>
public interface ITransaction : IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction. Disposing afterwards is a no-op.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);
}
