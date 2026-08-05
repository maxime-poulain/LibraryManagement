using Hangfire;
using LibraryManagement.Circulation.Application.Holds.ExpireUncollectedHolds;
using LibraryManagement.Circulation.Application.Holds.RemindOfHoldsExpiringSoon;
using LibraryManagement.Circulation.Application.Loans.DeclareOverdueLoansLost;
using LibraryManagement.Circulation.Application.Loans.RemindOfLoansDueSoon;
using LibraryManagement.Circulation.Application.Loans.RemindOfOverdueLoans;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Composition.Tests.Scheduling;

/// <summary>
/// What a run of the daily process did.
/// </summary>
/// <param name="Dispatched">How many of the day's commands were dispatched.</param>
/// <param name="Refused">
/// How many came back with errors. Kept with the job by Hangfire, so a run that half-failed is
/// visible in the dashboard without anyone having to query a table.
/// </param>
public sealed record DailyRunOutcome(int Dispatched, int Refused);

/// <summary>
/// The composition root's second Hangfire-facing surface, beside <c>OutboxJobs</c>: the daily
/// process Circulation's tactical design gives it, because the passage of time is not an event and
/// something has to ask the question.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The fan-out lives here and not in a handler.</strong> An architecture rule forbids a
/// command handler from depending on <see cref="ICommandDispatcher"/> — a nested dispatch shares
/// the scope and the store, and would turn five transactions into one. So the host dispatches the
/// five, and the module keeps deciding what each of them means.
/// </para>
/// <para>
/// <strong>One scope and one save each</strong>, exactly as the outbox drain opens a scope per
/// message. Five independent transactions: a failure in the reminders must not keep the hold shelf
/// from turning, which is why the run continues after a refusal and reports the count rather than
/// stopping at the first.
/// </para>
/// <para>
/// The order is the design's own table, and it is the order a borrower would want to hear things
/// in: due soon, then late, then written off. The two hold moments follow, warning before expiry,
/// though nothing depends on that — a claim past its deadline is expired, never hurried along.
/// </para>
/// <para>
/// A real host registers this with <c>RecurringJob.AddOrUpdate</c> on <c>Cron.Daily()</c> under
/// <see cref="JobId"/>. Daily and not minutely, unlike the drains: this run asks about days, and
/// asking twice in one day is safe but pointless — every command it dispatches is idempotent.
/// </para>
/// </remarks>
public sealed class CirculationDailyRun(IServiceScopeFactory scopes)
{
    /// <summary>The recurring job identifier the host registers the daily run under.</summary>
    public const string JobId = "circulation-daily";

    private static readonly ICommand<Result>[] TheDaysWork =
    [
        new RemindOfLoansDueSoonCommand(),
        new RemindOfOverdueLoansCommand(),
        new DeclareOverdueLoansLostCommand(),
        new RemindOfHoldsExpiringSoonCommand(),
        new ExpireUncollectedHoldsCommand(),
    ];

    /// <summary>
    /// Runs the day's five moments.
    /// </summary>
    /// <param name="cancellationToken">
    /// Replaced by Hangfire at execution time with the server's shutdown token.
    /// </param>
    /// <returns>What the run did.</returns>
    /// <remarks>
    /// <see cref="DisableConcurrentExecutionAttribute"/> for the reason the drains carry it: the
    /// commands would survive an overlap — they are idempotent — but there is no reason to
    /// exercise that tolerance on a schedule.
    /// </remarks>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task<DailyRunOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var refused = 0;

        foreach (var command in TheDaysWork)
        {
            await using var scope = scopes.CreateAsyncScope();

            var outcome = await scope.ServiceProvider
                .GetRequiredService<ICommandDispatcher>()
                .DispatchAsync(command, cancellationToken)
                .ConfigureAwait(false);

            if (outcome.HasErrors())
            {
                refused++;
            }
        }

        return new DailyRunOutcome(TheDaysWork.Length, refused);
    }
}
