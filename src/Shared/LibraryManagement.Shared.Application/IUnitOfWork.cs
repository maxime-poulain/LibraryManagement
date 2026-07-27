namespace LibraryManagement.Shared.Application;

/// <summary>
/// Writes everything a command changed, in one go. Implemented by each module over its own
/// persistence store; the shared kernel only declares the contract.
/// </summary>
/// <remarks>
/// <para>
/// A command is the unit of consistency: either everything it changed is persisted, or none of it
/// is. Nothing is written until this is called, because repositories only track — so a command that
/// reports a failure simply never reaches here, and there is nothing to undo.
/// </para>
/// <para>
/// <strong>There is deliberately no explicit transaction.</strong> One would guard against a
/// rollback that cannot happen: a single save is already atomic, Entity Framework wrapping its own
/// writes when no transaction is open. An explicit one would also be held for the whole duration of
/// the handler, reads included, keeping locks long after the work that needed them.
/// </para>
/// <para>
/// It would earn its place the day a command writes twice — two calls to this method, or a bulk
/// operation such as <c>ExecuteUpdateAsync</c> alongside tracked changes, which runs immediately and
/// outside the save. Neither happens today, and identifiers coming from the domain means no command
/// has a reason to save early to learn one. If that changes, add a transaction then rather than
/// keeping one now against a case that does not exist.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Writes every change the command made.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Throws when someone else changed an aggregate this command was working from. The dispatch
    /// pipeline turns that into a failed result: two employees acting on the same record at the
    /// same moment is an expected outcome of the business, not a defect. The exception type belongs
    /// to the persistence library, which this layer does not name.
    /// </remarks>
    ValueTask SaveChangesAsync(CancellationToken cancellationToken = default);
}
