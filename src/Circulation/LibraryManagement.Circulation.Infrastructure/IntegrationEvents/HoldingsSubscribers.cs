using LibraryManagement.Circulation.Application.Holds.ReleaseTrappedCopy;
using LibraryManagement.Circulation.Application.Loans.RecordLoanRecovery;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

// What this module hears from Holdings. Both subscribers do the same two things and nothing else:
// turn a flat contract into a command of this module, and refuse loudly if the command does.
// Neither writes — they run inside Holdings' drain, whose save is on Holdings' context, and
// dispatching puts the change in this module's own transaction.

/// <summary>
/// A copy left the lendable service, and any promise it carried must be taken back.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
public sealed class ReleasePromiseOnCopyLeftService(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<CopyLeftService>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        CopyLeftService contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(new ReleaseTrappedCopyCommand(contract.CopyId), cancellationToken)
            .ConfigureAwait(false);

        Refusal.Throw(result, $"release the promise on copy {contract.CopyId}", contract.EventId);
    }
}

/// <summary>
/// A written-off copy turned up, and what its lateness was worth can be settled at last.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
public sealed class RecordRecoveryOnCopyRecovered(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<CopyRecovered>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        CopyRecovered contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(new RecordLoanRecoveryCommand(contract.CopyId), cancellationToken)
            .ConfigureAwait(false);

        Refusal.Throw(result, $"record the recovery of copy {contract.CopyId}", contract.EventId);
    }
}

/// <summary>
/// Turns a refused command into the exception the drain needs to see.
/// </summary>
/// <remarks>
/// A refusal that returned quietly would let the announcing module mark its message delivered
/// while nothing happened. Throwing leaves the row unmarked, so the drain replays it and, if it
/// keeps failing, dead-letters it where an operator will find it.
/// </remarks>
internal static class Refusal
{
    internal static void Throw(
        LibraryManagement.Shared.Domain.Results.Result result,
        string attempt,
        Guid eventId)
    {
        var refusal = result.Match<string?>(
            () => null,
            errors => string.Join(" ", errors.Select(error => error.ToString())));

        if (refusal is not null)
        {
            throw new InvalidOperationException(
                $"Circulation could not {attempt}, announced in event {eventId}: {refusal}");
        }
    }
}
