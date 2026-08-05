using LibraryManagement.Shared.Application.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection;

namespace LibraryManagement.Shared.Infrastructure.IntegrationEvents;

/// <summary>
/// Delivers a published contract to every subscriber the container holds for it.
/// </summary>
/// <param name="services">
/// The scope the delivery runs in — the drain's scope for this message, so a subscriber's
/// dispatched command resolves the same scoped dependencies everything else in that message does.
/// </param>
/// <remarks>
/// <para>
/// Resolution is by the contract's own type and nothing else, because that is all a contract has: a
/// <c>*.PublishedLanguage</c> project references nothing, so there is no marker interface to
/// dispatch on and no mediator notification to register. <c>GetServices</c> over the closed generic
/// is the whole lookup.
/// </para>
/// <para>
/// <strong>Subscribers run in order and a failure stops the rest.</strong> Two modules reacting to
/// one fact are two independent transactions, so a subscriber that threw after another had already
/// committed leaves the announcement half-applied — and the replay that follows re-runs both, which
/// is exactly why every subscriber owes idempotence. Continuing past a failure would trade that
/// honest replay for a silent gap.
/// </para>
/// <para>
/// Nothing is caught here. The outbox processor is what turns a failed delivery into a recorded
/// attempt and eventually a dead letter; swallowing an exception at this level would report a
/// reaction that never happened, which is the one thing the outbox promises cannot occur.
/// </para>
/// </remarks>
public sealed class ContainerIntegrationEventPublisher(IServiceProvider services)
    : IIntegrationEventPublisher
{
    /// <inheritdoc/>
    public async ValueTask PublishAsync<TContract>(
        TContract contract,
        CancellationToken cancellationToken = default)
        where TContract : notnull
    {
        ArgumentNullException.ThrowIfNull(contract);

        foreach (var subscriber in services.GetServices<IIntegrationEventSubscriber<TContract>>())
        {
            await subscriber.HandleAsync(contract, cancellationToken).ConfigureAwait(false);
        }
    }
}
