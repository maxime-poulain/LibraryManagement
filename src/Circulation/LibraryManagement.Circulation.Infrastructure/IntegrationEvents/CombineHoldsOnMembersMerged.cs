using LibraryManagement.Circulation.Application.Holds.CombineHoldsOfMergedBorrower;
using LibraryManagement.Members.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

/// <summary>
/// Two member records became one, so the claims they held apart are one person's claims — combined
/// in every queue, under the survival rules both merges share.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <remarks>
/// <para>
/// <strong>A subscriber of its own, and not a second dispatch inside the first.</strong> The loans
/// and the queues are separate units of consistency, and the publisher runs every subscriber
/// registered for a contract — the arrangement the edition merge already settled for this module.
/// A throw in either replays both, which both survive: each sweeps by the absorbed identifier,
/// which after the first delivery is on nothing live.
/// </para>
/// <para>
/// <strong>It dispatches rather than writes</strong>, for the reason every subscriber here gives:
/// this runs inside Members' drain, whose save is on Members' context, so a queue changed here
/// would be tracked where nobody saves.
/// </para>
/// </remarks>
public sealed class CombineHoldsOnMembersMerged(ICommandDispatcher commands)
    : IIntegrationEventSubscriber<MembersMerged>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        MembersMerged contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var result = await commands
            .DispatchAsync(
                new CombineHoldsOfMergedBorrowerCommand(
                    contract.AbsorbedMemberId,
                    contract.SurvivingMemberId),
                cancellationToken)
            .ConfigureAwait(false);

        var refusal = result.Match<string?>(
            () => null,
            errors => string.Join(" ", errors.Select(error => error.ToString())));

        if (refusal is not null)
        {
            throw new InvalidOperationException(
                $"Circulation refused to combine the holds of member {contract.AbsorbedMemberId} "
                + $"with {contract.SurvivingMemberId}'s, which Members merged in event "
                + $"{contract.EventId}: {refusal}");
        }
    }
}
