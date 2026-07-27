using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Shared.Infrastructure.Behaviors;

/// <summary>
/// Writes what a command changed, once its handler has reported success.
/// </summary>
/// <typeparam name="TMessage">Any command. Queries do not reach this behavior.</typeparam>
/// <typeparam name="TResponse">Its result.</typeparam>
/// <param name="unitsOfWork">Finds the unit of work of the module that owns the command.</param>
/// <remarks>
/// <para>
/// Constrained to <see cref="ICommandBase"/>, so a query is never wrapped by it — a query changes
/// nothing and has nothing to write. That constraint is honoured by the container rather than by a
/// runtime check, and a test in the composition project pins it.
/// </para>
/// <para>
/// Writing only on success is the whole guarantee. Nothing is written before this point, because
/// repositories only track, so a command that reports a failure leaves the store untouched without
/// anything having to be undone. There is no explicit transaction, and
/// <see cref="IUnitOfWork"/> records why.
/// </para>
/// <para>
/// A concurrency conflict leaves through the same channel as every other expected failure. Two
/// employees acting on the same aggregate at the same moment is an outcome of the business, not a
/// defect, and a caller should not have to catch an exception to learn about it.
/// </para>
/// </remarks>
public sealed class UnitOfWorkBehavior<TMessage, TResponse>(IUnitOfWorkResolver unitsOfWork)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : ICommandBase
    where TResponse : IFailable<TResponse>
{
    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await next(message, cancellationToken).ConfigureAwait(false);

            if (!response.HasErrors())
            {
                await unitsOfWork.Resolve(message)
                    .SaveChangesAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return response;
        }
        catch (DbUpdateConcurrencyException)
        {
            return TResponse.Failure([
                new Error(
                    SharedErrorCodes.ConcurrencyConflict,
                    "The data was changed by someone else while this operation was running. "
                    + "Reload it and try again.")
            ]);
        }
    }
}
