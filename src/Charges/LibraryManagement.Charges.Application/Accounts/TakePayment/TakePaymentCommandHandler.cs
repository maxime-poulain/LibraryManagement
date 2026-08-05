using LibraryManagement.Charges.Domain;
using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.TakePayment;

/// <summary>
/// Handles <see cref="TakePaymentCommand"/>.
/// </summary>
/// <param name="accounts">The store holding the account.</param>
/// <remarks>
/// Unlike the two charges, this one refuses when there is no account: a member who owes nothing
/// cannot pay, and answering success would leave a librarian believing money was recorded. The
/// asymmetry is deliberate — a charge arriving for a member with no account is ordinary, a payment
/// arriving for one is a mistake at the desk.
/// </remarks>
public sealed class TakePaymentCommandHandler(IMemberAccountRepository accounts)
    : ICommandHandler<TakePaymentCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        TakePaymentCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var memberId = MemberId.Create(command.MemberId);
        var account = await accounts.GetByMemberAsync(memberId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return Result.Failure(
                ChargesErrorCodes.AccountNotFound,
                $"'{memberId}' owes nothing, so there is nothing to pay.");
        }

        return account.TakePayment(Money.Of(command.Amount));
    }
}
