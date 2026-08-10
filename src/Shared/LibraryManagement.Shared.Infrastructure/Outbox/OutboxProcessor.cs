using LibraryManagement.Shared.Application.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// Drains one module's outbox: delivers each pending event to its handlers, oldest first.
/// </summary>
/// <typeparam name="TContext">The module's context, which owns the table being drained.</typeparam>
/// <param name="scopes">Creates one scope per message, so each delivery gets fresh dependencies.</param>
/// <param name="logger">Where the drain reports what it delivered and what blocked it.</param>
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
/// A failure is also a <em>log line</em> — Warning while attempts remain, Error when the message
/// dies — carrying the message id, the event id and the type, never the payload. The row records
/// the failure, but nobody watches a table: the line is what an operator's alerting hooks, and the
/// event id in it is the correlation token that follows an event across the asynchronous boundary.
/// </para>
/// <para>
/// Delivery is at-least-once — a crash between the handler's save and nothing (they are the same
/// save) cannot lose a message, but a redelivery after a partial failure elsewhere remains
/// possible, and <c>IDomainEvent.EventId</c> is what a handler deduplicates by.
/// </para>
/// <para>
/// Processed rows accumulate, and <see cref="OutboxPurge{TContext}"/> is what removes them. The
/// drain never deletes: a run that both delivered and cleaned would tie how long history is kept to
/// how often the queue is emptied, and the two answer to different things — one to the reader's
/// latency, the other to what the library may keep about a member.
/// </para>
/// </remarks>
public sealed class OutboxProcessor<TContext>(
    IServiceScopeFactory scopes,
    ILogger<OutboxProcessor<TContext>> logger)
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

                if (blockage.Dead)
                {
                    OutboxLog.MessageDead(
                        logger, message.Id, message.EventId, message.Type, blockage.Attempts, exception);
                }
                else
                {
                    OutboxLog.HeadBlocked(
                        logger, message.Id, message.EventId, message.Type, blockage.Attempts, MaxAttempts, exception);
                }

                ReportDelivered(processed);

                return new OutboxDrainOutcome(processed, blockage);
            }
        }

        ReportDelivered(processed);

        return new OutboxDrainOutcome(processed, Blockage: null);
    }

    // An idle tick says nothing: the drain runs every minute forever, and a line per silence would
    // bury the lines that matter.
    private void ReportDelivered(int processed)
    {
        if (processed > 0)
        {
            OutboxLog.Drained(logger, processed);
        }
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

/// <summary>
/// The drain's log lines, source-generated. A companion type because the processor is generic and
/// the <see cref="LoggerMessageAttribute"/> generator does not reach into generic types.
/// </summary>
internal static partial class OutboxLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Outbox drained: {Delivered} message(s) delivered.")]
    public static partial void Drained(ILogger logger, int delivered);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Outbox delivery failed and the head blocks the queue: message {MessageId}, "
                  + "event {EventId} ({EventType}), attempt {Attempt} of {MaxAttempts}.")]
    public static partial void HeadBlocked(
        ILogger logger, long messageId, Guid eventId, string eventType, int attempt, int maxAttempts,
        Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "Outbox message is dead after {Attempts} attempts: message {MessageId}, "
                  + "event {EventId} ({EventType}). The queue moves on; the row keeps the failure.")]
    public static partial void MessageDead(
        ILogger logger, long messageId, Guid eventId, string eventType, int attempts,
        Exception exception);
}
