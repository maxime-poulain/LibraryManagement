using Hangfire;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Shared.Infrastructure.Outbox;

namespace LibraryManagement.Composition.Tests.Outbox;

/// <summary>
/// The composition root's Hangfire-facing surface: one method per module, each delegating to that
/// module's <see cref="OutboxProcessor{TContext}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This class is the whole reason no project under <c>src/</c> references Hangfire. The processor
/// is a plain class; putting it on a clock is the host's decision, and this thin wrapper is where
/// the host makes it — today the composition tests, later the real host, which will register it
/// with <c>RecurringJob.AddOrUpdate</c> per module on <c>Cron.Minutely()</c>, Hangfire's floor. A
/// minute of drain latency is comfortably inside the boundary test the strategic design is built
/// on: a few seconds of disagreement no librarian notices.
/// </para>
/// <para>
/// <strong>The recurring tick is also the retry.</strong> A blocked head is simply retried by the
/// next drain, so a transient failure waits at most a minute and a poisonous one dies after about
/// five. A nearer retry through <c>IBackgroundJobClient.Schedule</c> was built and then removed:
/// its backoff ladder converged to the tick's own period by the third step, the schedule was not
/// transactional with the failure it reacted to — so the tick stayed load-bearing anyway — and
/// every handler is asynchronous by design, so nothing anyone can see was waiting on the seconds
/// it saved. It would earn its place back if the heartbeat ever slowed far below the minute, or if
/// a handler's latency ever became something the business feels; add it then, not now. The
/// processor still reports each run's <see cref="OutboxDrainOutcome"/>, so the day the trigger
/// wants to act on a blockage, the information is already in its hands.
/// </para>
/// <para>
/// <see cref="DisableConcurrentExecutionAttribute"/> is what keeps two overlapping runs from
/// draining the same table at once. The processor itself would survive it — delivery is
/// at-least-once and handlers deduplicate by <c>EventId</c> — but there is no reason to exercise
/// that tolerance on a schedule.
/// </para>
/// </remarks>
public sealed class OutboxJobs(
    OutboxProcessor<CatalogDbContext> catalog,
    OutboxProcessor<HoldingsDbContext> holdings,
    OutboxProcessor<MembersDbContext> members,
    OutboxProcessor<CirculationDbContext> circulation)
{
    /// <summary>The recurring job identifier the host registers the Catalog drain under.</summary>
    public const string CatalogJobId = "catalog-outbox";

    /// <summary>The recurring job identifier the host registers the Holdings drain under.</summary>
    public const string HoldingsJobId = "holdings-outbox";

    /// <summary>The recurring job identifier the host registers the Members drain under.</summary>
    public const string MembersJobId = "members-outbox";

    /// <summary>The recurring job identifier the host registers the Circulation drain under.</summary>
    public const string CirculationJobId = "circulation-outbox";

    /// <summary>
    /// Drains the Catalog module's outbox.
    /// </summary>
    /// <param name="cancellationToken">
    /// Replaced by Hangfire at execution time with the server's shutdown token.
    /// </param>
    /// <returns>
    /// The run's outcome, which Hangfire keeps with the job — a blockage is thereby visible in the
    /// dashboard without anyone having to query the table.
    /// </returns>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public Task<OutboxDrainOutcome> DrainCatalogAsync(CancellationToken cancellationToken)
        => catalog.ProcessAsync(cancellationToken);

    /// <summary>
    /// Drains the Holdings module's outbox.
    /// </summary>
    /// <param name="cancellationToken">
    /// Replaced by Hangfire at execution time with the server's shutdown token.
    /// </param>
    /// <returns>The run's outcome.</returns>
    /// <remarks>
    /// A second method and a second job, not a loop over the modules. Each module's table is drained
    /// on its own schedule, blocks on its own head, and fails on its own — which is the whole point
    /// of a table per module, and would be undone by a single job whose first poisonous message
    /// stopped every other module's queue too.
    /// </remarks>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public Task<OutboxDrainOutcome> DrainHoldingsAsync(CancellationToken cancellationToken)
        => holdings.ProcessAsync(cancellationToken);

    /// <summary>
    /// Drains the Members module's outbox.
    /// </summary>
    /// <param name="cancellationToken">
    /// Replaced by Hangfire at execution time with the server's shutdown token.
    /// </param>
    /// <returns>The run's outcome.</returns>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public Task<OutboxDrainOutcome> DrainMembersAsync(CancellationToken cancellationToken)
        => members.ProcessAsync(cancellationToken);

    /// <summary>
    /// Drains the Circulation module's outbox.
    /// </summary>
    /// <param name="cancellationToken">
    /// Replaced by Hangfire at execution time with the server's shutdown token.
    /// </param>
    /// <returns>The run's outcome.</returns>
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public Task<OutboxDrainOutcome> DrainCirculationAsync(CancellationToken cancellationToken)
        => circulation.ProcessAsync(cancellationToken);
}
