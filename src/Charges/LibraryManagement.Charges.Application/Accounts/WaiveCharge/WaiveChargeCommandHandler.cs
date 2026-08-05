using LibraryManagement.Charges.Domain;
using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.WaiveCharge;

/// <summary>
/// Handles <see cref="WaiveChargeCommand"/>.
/// </summary>
/// <param name="accounts">The store holding the account.</param>
public sealed class WaiveChargeCommandHandler(IMemberAccountRepository accounts)
    : ICommandHandler<WaiveChargeCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        WaiveChargeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var memberId = MemberId.Create(command.MemberId);
        var account = await accounts.GetByMemberAsync(memberId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            return Result.Failure(
                ChargesErrorCodes.AccountNotFound,
                $"'{memberId}' owes nothing, so there is nothing to waive.");
        }

        return account.Waive(ChargeId.Create(command.ChargeId));
    }
}
