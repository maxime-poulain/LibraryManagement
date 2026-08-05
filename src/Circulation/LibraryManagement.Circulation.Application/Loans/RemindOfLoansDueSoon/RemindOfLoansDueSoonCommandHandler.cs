using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RemindOfLoansDueSoon;

/// <summary>
/// Handles <see cref="RemindOfLoansDueSoonCommand"/>.
/// </summary>
/// <param name="loans">The loans falling due within the courtesy window.</param>
/// <param name="queues">Consulted per loan, because what the message should ask for depends on
/// whether anybody is waiting for the edition.</param>
/// <param name="policy">The circulation policy, which decides how far ahead the courtesy looks.</param>
/// <param name="clock">The host's clock. The passage of time is not an event — this is the
/// something that asks the question.</param>
/// <remarks>
/// The store narrows to the loans the day could concern; each loan decides whether it has already
/// been announced. One query per loan for the queue is the cost of a useful message, and at
/// library scale a day's courtesy reminders are a handful.
/// </remarks>
public sealed class RemindOfLoansDueSoonCommandHandler(
    ILoanRepository loans,
    IHoldQueueRepository queues,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<RemindOfLoansDueSoonCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RemindOfLoansDueSoonCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var today = clock.Today();

        var dueSoon = await loans
            .ActiveDueBetweenAsync(
                today,
                today.AddDays(policy.CourtesyReminderDaysBeforeDue),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var loan in dueSoon)
        {
            var queue = await queues.GetByEditionAsync(loan.EditionId, cancellationToken)
                .ConfigureAwait(false);

            loan.RemindOfDueDate(today, queue?.AnyoneIsWaiting ?? false, policy);
        }

        return Result.Success();
    }
}
