using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.Application.Copies.NoteCopyAccountedFor;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Holdings.Infrastructure.IntegrationEvents;

/// <summary>
/// A copy that passed over the desk cannot be unaccounted for, and this is where the record is
/// made to agree with the shelf.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <remarks>
/// <para>
/// This module's tactical design long said a return changes nothing in Holdings, and for the
/// record it keeps that was right — who has a copy is a circulation fact. What the sentence
/// missed is the copy this module holds as <em>lost</em> while a member hands it back: nothing
/// signalled the contradiction, and the record said <em>unaccounted for</em> about an object in
/// hand until a checkout attempt happened to trip over it. The reversal is argued in the
/// tactical design where the old sentence stood.
/// </para>
/// <para>
/// Almost every delivery is a no-op, which is the price of announcing rather than telling: the
/// publisher cannot know which returns matter without knowing this module's statuses — the exact
/// knowledge the boundary exists to withhold. The same price <c>MemberBalanceChanged</c> pays in
/// the other cycle, for the same reason.
/// </para>
/// </remarks>
public sealed class NoteCopyAccountedForOnCopyReturned(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<CopyReturned>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        CopyReturned contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(new NoteCopyAccountedForCommand(contract.CopyId), cancellationToken)
            .ConfigureAwait(false);

        var refusal = result.Match<string?>(
            () => null,
            errors => string.Join(" ", errors.Select(error => error.ToString())));

        if (refusal is not null)
        {
            throw new InvalidOperationException(
                $"Holdings could not account for copy {contract.CopyId}, whose return was "
                + $"announced in event {contract.EventId}: {refusal}");
        }
    }
}
