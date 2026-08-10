using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LibraryManagement.Shared.Infrastructure.Outbox;

/// <summary>
/// Deletes one module's settled outbox rows once they are older than a retention window.
/// </summary>
/// <typeparam name="TContext">The module's context, which owns the table being purged.</typeparam>
/// <param name="scopes">Creates the scope the deletes run in, as the drain does.</param>
/// <param name="clock">Where the cutoff comes from.</param>
/// <param name="logger">Where the purge reports what it removed.</param>
/// <remarks>
/// <para>
/// <strong>The purge is a requirement, not housekeeping.</strong> A payload holds the event as it
/// was raised, so an outbox row written before a member was erased still carries their name after
/// the erasure — a second store of personal data the library never decided to keep. Members §10
/// records that gap; this closes it. How long the window is stays the host's call, as the scheduler
/// and the log sinks are; <em>that</em> there is one is not.
/// </para>
/// <para>
/// <strong>Dead rows go too, and that is the harder half.</strong> A dead letter stays in the table
/// because it is an operator's problem, and a problem that vanished is not solved — but "kept for an
/// operator" cannot mean "kept forever", or the rule that makes erasure real has an exception big
/// enough to walk through. So the window is the operator's response time, and its end is announced:
/// a run that removes dead rows says so at Warning with the count, which is a failure closing
/// unresolved rather than a failure disappearing.
/// </para>
/// <para>
/// A plain class with no scheduler in it, like <see cref="OutboxProcessor{TContext}"/> — what puts
/// it on a clock is the host's decision. The deletes go straight to the store rather than through
/// the change tracker: nothing here needs materializing, and the rows are the interceptors' own
/// bookkeeping rather than anybody's aggregate.
/// </para>
/// </remarks>
public sealed class OutboxPurge<TContext>(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<OutboxPurge<TContext>> logger)
    where TContext : DbContext
{
    /// <summary>
    /// Removes every message settled before the window opens — delivered or given up on.
    /// </summary>
    /// <param name="retention">How far back settled rows are kept.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>What the run removed, counted apart because the two counts mean different things.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="retention"/> is negative, which would delete rows the drain has
    /// not reached yet.
    /// </exception>
    public async Task<OutboxPurgeOutcome> PurgeAsync(
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(retention, TimeSpan.Zero);

        var cutoff = clock.GetUtcNow() - retention;

        var scope = scopes.CreateAsyncScope();
        await using var _ = scope.ConfigureAwait(false);
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        // Two statements rather than one predicate over both columns: a delivered message and one
        // nobody could deliver are different facts, and a single count would hide the second behind
        // the first — which is exactly the one an operator needs to hear about.
        var delivered = await context.Set<OutboxMessage>()
            .Where(message => message.ProcessedOn != null && message.ProcessedOn < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var dead = await context.Set<OutboxMessage>()
            .Where(message => message.DeadOn != null && message.DeadOn < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dead > 0)
        {
            OutboxPurgeLog.DeadRemoved(logger, dead, typeof(TContext).Name, retention);
        }
        else if (delivered > 0)
        {
            OutboxPurgeLog.Purged(logger, delivered, typeof(TContext).Name);
        }

        return new OutboxPurgeOutcome(delivered, dead);
    }
}

/// <summary>
/// The purge's log lines, source-generated — a companion type for the reason the drain's is one:
/// the generator does not reach into generic types.
/// </summary>
internal static partial class OutboxPurgeLog
{
    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Outbox purged: {Delivered} delivered message(s) removed from {Store}.")]
    public static partial void Purged(ILogger logger, int delivered, string store);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "Outbox purge removed {Dead} dead message(s) from {Store}: they were never "
                  + "delivered and are now past the {Retention} retention window, so the failures "
                  + "they recorded are gone unresolved.")]
    public static partial void DeadRemoved(ILogger logger, int dead, string store, TimeSpan retention);
}
