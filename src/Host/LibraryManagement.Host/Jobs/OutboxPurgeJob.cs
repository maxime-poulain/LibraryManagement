using Hangfire;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Circulation.Infrastructure.Persistence;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Shared.Infrastructure.Outbox;
using Microsoft.Extensions.Options;

namespace LibraryManagement.Host.Jobs;

/// <summary>
/// How long a settled outbox row is kept before the purge removes it.
/// </summary>
/// <remarks>
/// The host's call, as <c>outbox.md</c> §10 says it is — and a deliberate one: thirty days is long
/// enough to answer "was this delivered, and when?" about anything anyone still remembers, and to
/// give an operator a month with a dead letter before it goes. That it exists at all is not the
/// host's call, which is why the mechanism is registered per module and only the number lives here.
/// </remarks>
public sealed class OutboxRetention
{
    /// <summary>The configuration section this binds to.</summary>
    public const string Section = "Outbox";

    /// <summary>Days a delivered or dead row is kept.</summary>
    public int PurgeWindowDays { get; init; } = 30;
}

/// <summary>
/// The host's third Hangfire-facing surface: it empties every module's outbox of rows that have
/// settled and outlived the retention window.
/// </summary>
/// <param name="catalog">The Catalog module's purge.</param>
/// <param name="holdings">The Holdings module's purge.</param>
/// <param name="members">The Members module's purge.</param>
/// <param name="circulation">The Circulation module's purge.</param>
/// <param name="charges">The Charges module's purge.</param>
/// <param name="retention">How long settled rows are kept.</param>
/// <remarks>
/// <para>
/// <strong>One job for five modules, where the drains are five jobs.</strong> The drains are split
/// because each table has a head that blocks, and one poisonous message must not stop four other
/// queues. A purge has no head and no order to protect: it deletes rows nobody is waiting on, so a
/// failure costs the modules behind it one day's delay and nothing else. Splitting it would buy
/// isolation nobody needs and put five schedules where one belongs.
/// </para>
/// <para>
/// Daily, because the window is measured in days: running it more often would delete the same rows
/// a few hours earlier, and running it less often would make the window mean something other than
/// what it says.
/// </para>
/// </remarks>
public sealed class OutboxPurgeJob(
    OutboxPurge<CatalogDbContext> catalog,
    OutboxPurge<HoldingsDbContext> holdings,
    OutboxPurge<MembersDbContext> members,
    OutboxPurge<CirculationDbContext> circulation,
    OutboxPurge<ChargesDbContext> charges,
    IOptions<OutboxRetention> retention)
{
    /// <summary>The recurring job identifier the host registers the purge under.</summary>
    public const string JobId = "outbox-purge";

    /// <summary>
    /// Purges every module's outbox, oldest settled rows first.
    /// </summary>
    /// <param name="cancellationToken">
    /// Replaced by Hangfire at execution time with the server's shutdown token.
    /// </param>
    /// <returns>What each module's purge removed, summed — the number Hangfire keeps with the job.</returns>
    /// <remarks>
    /// <see cref="DisableConcurrentExecutionAttribute"/> for the reason the drains carry it. Two
    /// overlapping purges would be harmless — deleting a row already deleted removes nothing — but
    /// there is no reason to exercise that tolerance on a schedule.
    /// </remarks>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task<OutboxPurgeOutcome> PurgeAsync(CancellationToken cancellationToken)
    {
        var window = TimeSpan.FromDays(retention.Value.PurgeWindowDays);

        var delivered = 0;
        var dead = 0;

        foreach (var purge in Purges())
        {
            var outcome = await purge(window, cancellationToken).ConfigureAwait(false);
            delivered += outcome.Delivered;
            dead += outcome.Dead;
        }

        return new OutboxPurgeOutcome(delivered, dead);
    }

    // The five, in the order the modules appear on the context map. Sequential and not parallel:
    // they contend for one database, and a purge is the least urgent thing the host does.
    private IEnumerable<Func<TimeSpan, CancellationToken, Task<OutboxPurgeOutcome>>> Purges()
    {
        yield return catalog.PurgeAsync;
        yield return holdings.PurgeAsync;
        yield return members.PurgeAsync;
        yield return circulation.PurgeAsync;
        yield return charges.PurgeAsync;
    }
}
