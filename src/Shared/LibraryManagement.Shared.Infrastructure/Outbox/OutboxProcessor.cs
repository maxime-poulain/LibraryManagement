using LibraryManagement.Shared.Application.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// Drains one module's outbox: delivers each pending event to its handlers, oldest first.
/// </summary>
/// <typeparam name="TContext">The module's context, which owns the table being drained.</typeparam>
/// <param name="scopes">Creates one scope per message, so each delivery gets fresh dependencies.</param>
/// <remarks>
/// <para>
/// A plain class with no scheduler in it. Whatever triggers a run — Hangfire in the composition
/// root, a test calling it directly — is the host's decision, exactly as the database provider is.
/// </para>
/// <para>
/// <strong>One message, one scope, one save.</strong> The handler's writes, the row's
/// <see cref="OutboxMessage.ProcessedOn"/> mark, and any events the handler itself raised — turned
/// into new rows by <see cref="OutboxInterceptor"/> — leave in a single <c>SaveChangesAsync</c>.
/// Within the module that is effectively exactly-once: a message is never marked without its
/// effects, and never takes effect without being marked. Chains drain naturally, one message at a
/// time, within the same run.
/// </para>
/// <para>
/// <strong>Order is the contract, so a failure blocks the head.</strong> The design of Circulation
/// leans on causal order — the debt settles before the copy is trapped — and a queue that skipped a
/// failing message would reorder exactly when things are already going wrong. A failure is recorded,
/// the run stops, and the next run — the recurring drain is also the retry — takes the same head
/// again. After <see cref="MaxAttempts"/> the message is marked dead and skipped, and the queue
/// moves again. A dead letter is an operator's problem, loudly recorded rather than silently
/// reordered around. The blockage travels out in the <see cref="OutboxDrainOutcome"/>, so a trigger
/// that ever wants to act on it — a host with a far slower heartbeat scheduling a nearer retry —
/// already has what it needs; retrying the head is just draining again.
/// </para>
/// <para>
/// Delivery is at-least-once — a crash between the handler's save and nothing (they are the same
/// save) cannot lose a message, but a redelivery after a partial failure elsewhere remains
/// possible, and <c>IDomainEvent.EventId</c> is what a handler deduplicates by.
/// </para>
/// <para>
/// Processed rows accumulate; a purge policy is deliberately deferred to the host, alongside the
/// scheduler that will own it.
/// </para>
/// </remarks>
public sealed class OutboxProcessor<TContext>(IServiceScopeFactory scopes)
    where TContext : DbContext
{
    /// <summary>
    /// How many failed deliveries a message is allowed before it is marked dead and skipped.
    /// </summary>
    public const int MaxAttempts = 5;

    // Bounds one run, so a scheduled drain cannot monopolise its worker behind a deep backlog. The
    // next run picks up where this one stopped.
    private const int BatchLimit = 100;

    /// <summary>
    /// Delivers pending messages, oldest first, until the outbox is empty, a delivery fails, or the
    /// batch limit is reached.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// What the run did, and — when a delivery failed — the blockage that ended it. The processor
    /// reports and never decides: what a trigger does with a blockage — record it, alert on it,
    /// or, under a host whose heartbeat is far slower than a minute, schedule a nearer retry — is
    /// the host's business.
    /// </returns>
    public async Task<OutboxDrainOutcome> ProcessAsync(CancellationToken cancellationToken = default)
    {
        var processed = 0;

        for (var i = 0; i < BatchLimit; i++)
        {
            var scope = scopes.CreateAsyncScope();
            await using var _ = scope.ConfigureAwait(false);
            var context = scope.ServiceProvider.GetRequiredService<TContext>();

            var message = await PendingOf(context)
                .OrderBy(pending => pending.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (message is null)
            {
                break;
            }

            try
            {
                var serializer = scope.ServiceProvider.GetRequiredService<IDomainEventSerializer>();
                var publisher = scope.ServiceProvider.GetRequiredService<IDomainEventPublisher>();
                var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

                var domainEvent = serializer.Deserialize(message.Type, message.Payload);
                await publisher.PublishAsync(domainEvent, cancellationToken).ConfigureAwait(false);

                // Marked and saved with everything the handler changed: the mark is only ever
                // written alongside the effects it stands for.
                message.ProcessedOn = clock.GetUtcNow();
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                processed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Handlers are arbitrary code; the queue has to survive whatever they throw. The
                // one exception passed through above is cancellation, which is the caller's, not a
                // failure of the message.
                var blockage = await RecordFailureAsync(message.Id, exception, cancellationToken)
                    .ConfigureAwait(false);

                return new OutboxDrainOutcome(processed, blockage);
            }
        }

        return new OutboxDrainOutcome(processed, Blockage: null);
    }

    // On a fresh scope, deliberately: the scope the handler failed in may hold half of that
    // handler's changes, and saving the bookkeeping there would save the half along with it.
    private async Task<OutboxBlockage> RecordFailureAsync(
        long messageId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using var _ = scope.ConfigureAwait(false);
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var message = await context.Set<OutboxMessage>()
            .FirstAsync(failed => failed.Id == messageId, cancellationToken)
            .ConfigureAwait(false);

        message.Attempts++;
        message.Error = exception.ToString();

        var dead = message.Attempts >= MaxAttempts;

        if (dead)
        {
            message.DeadOn = clock.GetUtcNow();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new OutboxBlockage(message.Id, message.Attempts, dead);
    }

    private static IQueryable<OutboxMessage> PendingOf(TContext context)
        => context.Set<OutboxMessage>()
            .Where(message => message.ProcessedOn == null && message.DeadOn == null);
}
