using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Application.IntegrationEvents;
using Contracts = LibraryManagement.Members.PublishedLanguage;

namespace LibraryManagement.Members.Infrastructure.IntegrationEvents;

/// <summary>
/// Flattens the merge into the fact the modules downstream of this one are allowed to see.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
/// <remarks>
/// <para>
/// The first translator in this module, and the first thing Members says on its own initiative.
/// Everything it published before was an answer to a question somebody asked it.
/// </para>
/// <para>
/// The translation happens here, on the announcing side, because it can happen nowhere else: a
/// consuming module may not reference <c>Members.Domain</c>, so it could not name
/// <see cref="MembersMerged"/> to subscribe to it.
/// </para>
/// <para>
/// The domain event and the contract carry the same two identifiers, so this flattening looks like a
/// formality — and it is not one. The domain event carries <c>MemberId</c>, a type that belongs to
/// this context and is free to change; the contract carries <c>Guid</c>, which is not.
/// </para>
/// </remarks>
public sealed class MembersMergedTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<MembersMerged>
{
    /// <inheritdoc/>
    public async ValueTask Handle(MembersMerged notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new Contracts.MembersMerged(
                notification.EventId,
                notification.AbsorbedMemberId.Value,
                notification.SurvivingMemberId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}
