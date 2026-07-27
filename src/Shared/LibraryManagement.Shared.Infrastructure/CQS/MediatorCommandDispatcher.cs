using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

// Mediator declares its own ICommand<T> and IQuery<T>. Aliasing the one type needed from it keeps
// `ICommand<Result>` in this file unambiguously the kernel's own.
using ISender = Mediator.ISender;

namespace LibraryManagement.Shared.Infrastructure.CQS;

/// <summary>
/// Implements <see cref="ICommandDispatcher"/> over Mediator.
/// </summary>
/// <param name="sender">Routes the command through the pipeline to its handler.</param>
/// <remarks>
/// <para>
/// A delegation and nothing more. What used to live here — validating, opening a transaction,
/// translating a concurrency conflict — is now a pipeline behavior each, because that is what
/// composes: adding a concern is registering one more, in the right place in the order, instead of
/// surgery on this method.
/// </para>
/// <para>
/// It still earns its keep, thinly. <see cref="ICommandDispatcher"/> is the application layer's own
/// contract, so a caller depends on it and never on Mediator. That is the whole of what this type
/// is for, and it is enough.
/// </para>
/// </remarks>
public sealed class MediatorCommandDispatcher(ISender sender) : ICommandDispatcher
{
    /// <inheritdoc/>
    public ValueTask<Result> DispatchAsync(
        ICommand<Result> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return sender.Send(command, cancellationToken);
    }
}
