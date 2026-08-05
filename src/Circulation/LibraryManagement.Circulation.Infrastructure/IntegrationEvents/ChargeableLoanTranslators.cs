using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Shared.Application.DomainEvents;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

// The facts this context announces to whoever prices them. Both flatten a domain event into a
// record of primitives; neither knows who is listening, and neither carries a price — what
// lateness or a loss costs is decided elsewhere, which is the whole reason these are events and
// not calls.

/// <summary>
/// Announces a return, so it can be priced.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
/// <remarks>
/// <strong>Every return is announced, including the punctual ones.</strong> Filtering here on
/// <c>DaysLate</c> would put the tariff's grace period in this context: the day the library forgives
/// the first two days, the rule would live in the module that knows nothing about money. So the fact
/// leaves as it happened and the subscriber decides there is nothing to charge.
/// </remarks>
public sealed class LoanReturnedTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<LoanReturned>
{
    /// <inheritdoc/>
    public async ValueTask Handle(LoanReturned notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new LoanReturnedLate(
                notification.EventId,
                notification.BorrowerId.Value,
                notification.LoanId.Value,
                notification.CopyId.Value,
                notification.DaysLate),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Announces a loan given up on, so the replacement can be priced.
/// </summary>
/// <param name="announcements">Carries the flattened fact to whoever subscribed to it.</param>
/// <remarks>
/// The second translator on one domain event: <c>LoanDeclaredLost</c> already leaves this context as
/// <see cref="CopyReportedLost"/> for Holdings, carrying the copy and nothing else. This one carries
/// the borrower too, because somebody has to be asked for the money. Two flattenings of one fact,
/// each holding exactly what its audience has a use for.
/// </remarks>
public sealed class LoanDeclaredLostChargeTranslator(IIntegrationEventPublisher announcements)
    : IDomainEventHandler<LoanDeclaredLost>
{
    /// <inheritdoc/>
    public async ValueTask Handle(LoanDeclaredLost notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await announcements.PublishAsync(
            new LoanWrittenOff(
                notification.EventId,
                notification.BorrowerId.Value,
                notification.LoanId.Value,
                notification.CopyId.Value),
            cancellationToken).ConfigureAwait(false);
    }
}
