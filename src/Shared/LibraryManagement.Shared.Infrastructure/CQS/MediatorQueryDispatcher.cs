using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

// Mediator declares its own IQuery<T>. Aliasing the one type needed from it keeps
// `IQuery<TValue>` in this file unambiguously the kernel's own.
using ISender = Mediator.ISender;

namespace LibraryManagement.Shared.Infrastructure.CQS;

/// <summary>
/// Implements <see cref="IQueryDispatcher"/> over Mediator.
/// </summary>
/// <remarks>
/// Unlike <see cref="MediatorCommandDispatcher"/>, this one opens no transaction and translates no
/// exception: a query changes nothing, so it has nothing to commit and nothing to contend for.
/// The asymmetry between the two dispatchers is command-query separation made visible in the
/// infrastructure rather than merely asserted in the abstractions.
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
