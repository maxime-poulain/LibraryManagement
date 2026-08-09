using LibraryManagement.Charges.Application.Accounts.CancelReplacementCharge;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Charges.Infrastructure.IntegrationEvents;

/// <summary>
/// A copy this context priced the replacement of has turned up, and what is still owed for it is
/// cancelled.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <remarks>
/// The edge the tactical design carried as its one outstanding item: the aggregate's cancellation
/// and the copy recorded on every charge waited ready, and only this passage was missing. It
/// dispatches rather than writes — the drain's save is on Holdings' context — and a copy with
/// nothing outstanding answers success, which is both the paid-charge rule (§9 forbids refunds)
/// and what makes redelivery safe.
/// </remarks>
public sealed class CancelChargeOnCopyRecovered(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<CopyRecovered>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        CopyRecovered contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(new CancelReplacementChargeCommand(contract.CopyId), cancellationToken)
            .ConfigureAwait(false);

        Refusal.Throw(result, $"cancel what copy {contract.CopyId} still owed", contract.EventId);
    }
}
