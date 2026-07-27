using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

// Mediator declares its own IQuery<T>. Aliasing the one type needed from it keeps
// `IQuery<TValue>` in this file unambiguously the kernel's own.
using ISender = Mediator.ISender;

namespace LibraryManagement.Shared.Infrastructure.CQS;

/// <summary>
/// Implements <see cref="IQueryDispatcher"/> over Mediator.
/// </summary>
/// <param name="sender">Routes the query through the pipeline to its handler.</param>
/// <remarks>
/// Identical in shape to <see cref="MediatorCommandDispatcher"/>, and for the same reason: the
/// concerns that used to distinguish them are pipeline behaviors now. The asymmetry survives where
/// it belongs — <c>UnitOfWorkBehavior</c> is constrained to commands, so a query passes through
/// validation and nothing else.
/// </remarks>
public sealed class MediatorQueryDispatcher(ISender sender) : IQueryDispatcher
{
    /// <inheritdoc/>
    public ValueTask<Result<TValue>> DispatchAsync<TValue>(
        IQuery<TValue> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return sender.Send(query, cancellationToken);
    }
}
