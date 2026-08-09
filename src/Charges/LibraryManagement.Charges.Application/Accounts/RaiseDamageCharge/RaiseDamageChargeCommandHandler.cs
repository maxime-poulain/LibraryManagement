using LibraryManagement.Charges.Domain;
using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.RaiseDamageCharge;

/// <summary>
/// Handles <see cref="RaiseDamageChargeCommand"/>.
/// </summary>
/// <param name="accounts">The member's account, opened by its first charge if need be.</param>
/// <param name="policy">The tariff.</param>
/// <param name="clock">The host's clock.</param>
public sealed class RaiseDamageChargeCommandHandler(
    IMemberAccountRepository accounts,
    ChargesPolicy policy,
    TimeProvider clock) : ICommandHandler<RaiseDamageChargeCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RaiseDamageChargeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var memberId = MemberId.Create(command.MemberId);
        var account = await accounts.GetByMemberAsync(memberId, cancellationToken).ConfigureAwait(false);

        if (account is null)
        {
            account = MemberAccount.For(memberId);
            accounts.Add(account);
        }

        return account.RaiseDamageCharge(
            ChargeId.Generate(),
            LoanId.Create(command.LoanId),
            CopyId.Create(command.CopyId),
            clock.Today(),
            policy);
    }
}
