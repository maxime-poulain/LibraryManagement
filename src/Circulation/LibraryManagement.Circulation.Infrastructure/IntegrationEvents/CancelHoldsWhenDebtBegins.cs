using LibraryManagement.Charges.PublishedLanguage;
using LibraryManagement.Circulation.Application.Holds.CancelHoldsForDebt;
using LibraryManagement.Circulation.Domain;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Application.IntegrationEvents;

namespace LibraryManagement.Circulation.Infrastructure.IntegrationEvents;

/// <summary>
/// A member's balance moved, and this context decides whether that costs them their places.
/// </summary>
/// <param name="commands">Where the reaction is decided — this module's own use case.</param>
/// <param name="policy">The circulation policy, which is where the threshold lives.</param>
/// <remarks>
/// <para>
/// <strong>The judgement happens here and nowhere else.</strong> Charges announced two amounts and
/// no opinion; this applies <c>BlockingDebt</c> to the pair and acts only on a crossing upwards.
/// A movement that stays on one side of the line changes nothing, and so does a return to good
/// standing — a borrower whose balance clears is simply able to act again, and nothing in the model
/// has to be undone for that.
/// </para>
/// <para>
/// Because both amounts travel, nothing is remembered between deliveries: no standing is stored, no
/// history compared. The pair is the whole input.
/// </para>
/// <para>
/// <strong>This closes the one cycle the context map draws.</strong> Holdings and Members answer
/// this context synchronously at the desk; Charges is asked synchronously too, and answers back
/// asynchronously here. The inversion is what keeps the assembly graph acyclic while the
/// conversation runs both ways.
/// </para>
/// </remarks>
public sealed class CancelHoldsWhenDebtBegins(
    ICommandDispatcher commands,
    CirculationPolicy policy)
    : IIntegrationEventSubscriber<MemberBalanceChanged>
{
    /// <inheritdoc/>
    public async ValueTask HandleAsync(
        MemberBalanceChanged contract,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (Forbids(contract.CurrentBalance) && !Forbids(contract.PreviousBalance))
        {
            var result = await commands
                .DispatchAsync(new CancelHoldsForDebtCommand(contract.MemberId), cancellationToken)
                .ConfigureAwait(false);

            var refusal = result.Match<string?>(
                () => null,
                errors => string.Join(" ", errors.Select(error => error.ToString())));

            if (refusal is not null)
            {
                throw new InvalidOperationException(
                    $"Circulation could not cancel the holds of borrower {contract.MemberId}, "
                    + $"whose balance moved in event {contract.EventId}: {refusal}");
            }
        }
    }

    private bool Forbids(decimal balance) => policy.DebtForbids(balance);
}
