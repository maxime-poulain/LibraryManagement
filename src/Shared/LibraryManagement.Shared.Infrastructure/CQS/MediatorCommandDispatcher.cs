using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.Errors;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.EntityFrameworkCore;

// Mediator declares its own ICommand<T> and IQuery<T>. Aliasing the one type needed from it keeps
// `ICommand<Result>` in this file unambiguously the kernel's own.
using ISender = Mediator.ISender;

namespace LibraryManagement.Shared.Infrastructure.CQS;

/// <summary>
/// Implements <see cref="ICommandDispatcher"/> over Mediator, and makes the dispatch point carry
/// the three concerns every command shares: input validation, the transaction boundary, and the
/// translation of a concurrency conflict into a failed <see cref="Result"/>.
/// </summary>
/// <remarks>
/// <para>
/// Without those responsibilities this type would be a one-line delegation to <see cref="ISender"/>
/// and would earn nothing. With them, it is the only place that sees every command go by, which is
/// what a cross-cutting rule needs.
/// </para>
/// <para>
/// Order matters. Validation runs <em>before</em> the transaction is opened, so a command rejected
/// for a missing field never starts one. A Mediator pipeline behaviour could not do this: it runs
/// inside <see cref="ISender"/>, therefore inside the transaction already begun here.
/// </para>
/// <para>
/// The transaction is committed only when the command reports success. A command that returns a
/// failure has decided its work should not stand, so the transaction is discarded — the handler does
/// not have to remember to undo anything.
/// </para>
/// </remarks>
public sealed class MediatorCommandDispatcher(
    ISender sender,
    ITransactionManagerResolver transactionManagers,
    ICommandValidator validator) : ICommandDispatcher
{
    /// <inheritdoc/>
    public async ValueTask<Result> DispatchAsync(
        ICommand<Result> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationErrors = await validator
            .ValidateAsync(command, cancellationToken)
            .ConfigureAwait(false);

        if (validationErrors.HasErrors)
        {
            return Result.Failure(validationErrors);
        }

        // The command's own module owns the store its transaction belongs to. Resolving before the
        // try block keeps a wiring mistake an exception rather than a concurrency conflict.
        var transactionManager = transactionManagers.Resolve(command);

        try
        {
            var transaction = await transactionManager
                .BeginAsync(cancellationToken)
                .ConfigureAwait(false);

            // Leaving this scope without having committed rolls the transaction back.
            await using (transaction.ConfigureAwait(false))
            {
                var result = await sender.Send(command, cancellationToken).ConfigureAwait(false);

                if (result.Match(() => true, _ => false))
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                return result;
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two employees acted on the same aggregate at the same moment. That is an expected
            // outcome of the business, so it leaves through the Result channel like every other
            // expected failure rather than as an exception the caller has to know about.
            return Result.Failure(
                SharedErrorCodes.ConcurrencyConflict,
                "The data was changed by someone else while this operation was running. "
                + "Reload it and try again.");
        }
    }
}
