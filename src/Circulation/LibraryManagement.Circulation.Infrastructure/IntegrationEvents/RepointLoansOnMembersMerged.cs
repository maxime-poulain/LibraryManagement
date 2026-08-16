using LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedBorrower;
using LibraryManagement.Members.PublishedLanguage;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

/// <summary>
/// Two member records became one, so the loans the absorbed one has not yet answered for answer
/// to the survivor.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <remarks>
/// <para>
/// <strong>The first fact Members states on its own initiative, arriving at its first
/// consumer.</strong> This module has asked Members the entitlement question before every checkout
/// and every hold since the desk existed; this is the first time the conversation runs the other
/// way. The passage is the one every merge reaction here uses, because no constraint crosses a
/// schema and nothing but an event can carry the news that an identifier stored here stopped
/// meaning what it meant.
/// </para>
/// <para>
/// <strong>It dispatches rather than writes</strong>, for the reason every subscriber here gives:
/// this runs inside Members' drain, whose save is on Members' context, so a loan changed here
/// would be tracked where nobody saves.
/// </para>
/// <para>
/// <strong>A refusal throws</strong>, leaving the announcing row unmarked for the next run and,
/// failing repeatedly, for an operator. Redelivery is safe: the command sweeps by the absorbed
/// identifier, which after the first delivery answers for no unsettled loan.
/// </para>
/// </remarks>
public sealed class RepointLoansOnMembersMerged(ICommandDispatcher commands)
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
                new RepointLoansOfMergedBorrowerCommand(
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
                $"Circulation refused to repoint the loans of member {contract.AbsorbedMemberId} "
                + $"at {contract.SurvivingMemberId}, which Members merged in event "
                + $"{contract.EventId}: {refusal}");
        }
    }
}
