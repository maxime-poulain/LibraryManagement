using LibraryManagement.Charges.Application.Accounts.AssessOverdueFine;
using LibraryManagement.Charges.Application.Accounts.RaiseDamageCharge;
using LibraryManagement.Charges.Application.Accounts.RaiseReplacementCharge;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Infrastructure.IntegrationEvents;

// What this module listens to. Both subscribers do the same two things and nothing else: turn a
// flat contract into a command of this module, and refuse loudly if the command does.
//
// Neither writes. They run inside Circulation's drain, whose save is on Circulation's context, so a
// subscriber that touched an account directly would leave it tracked in a context nobody saves.
// Dispatching puts the change in this module's own transaction, decided by this module's handler.

/// <summary>
/// A copy came back, so any delay can be priced.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
public sealed class AssessFineOnLoanReturned(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<LoanReturned>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        LoanReturned contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(
                new AssessOverdueFineCommand(
                    contract.BorrowerId, contract.LoanId, contract.CopyId, contract.DaysLate),
                cancellationToken)
            .ConfigureAwait(false);

        Refusal.Throw(result, $"price the return of loan {contract.LoanId}", contract.EventId);
    }
}

/// <summary>
/// A copy came back spoiled, so the damage is priced.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
public sealed class RaiseChargeOnCopyReturnedDamaged(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<CopyReturnedDamaged>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        CopyReturnedDamaged contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(
                new RaiseDamageChargeCommand(contract.BorrowerId, contract.LoanId, contract.CopyId),
                cancellationToken)
            .ConfigureAwait(false);

        Refusal.Throw(result, $"price the damage loan {contract.LoanId} brought back", contract.EventId);
    }
}

/// <summary>
/// A copy will not come back, so its replacement is priced.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
public sealed class RaiseChargeOnLoanEndedUnreturned(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<LoanEndedUnreturned>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        LoanEndedUnreturned contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(
                new RaiseReplacementChargeCommand(
                    contract.BorrowerId, contract.LoanId, contract.CopyId),
                cancellationToken)
            .ConfigureAwait(false);

        Refusal.Throw(result, $"price the replacement of loan {contract.LoanId}", contract.EventId);
    }
}

/// <summary>
/// Turns a refused command into the exception the drain needs to see.
/// </summary>
/// <remarks>
/// A refusal that returned quietly would let the announcing module mark its message delivered while
/// nothing happened. Throwing leaves the row unmarked, so the drain replays it and, if it keeps
/// failing, dead-letters it where an operator will find it — which is the right end for a real
/// disagreement between two modules' records.
/// </remarks>
internal static class Refusal
{
    internal static void Throw(Result result, string attempt, Guid eventId)
    {
        var refusal = result.Match<string?>(
            () => null,
            errors => string.Join(" ", errors.Select(error => error.ToString())));

        if (refusal is not null)
        {
            throw new InvalidOperationException(
                $"Charges could not {attempt}, announced in event {eventId}: {refusal}");
        }
    }
}
