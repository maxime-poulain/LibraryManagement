using LibraryManagement.Charges.Domain;
using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.RaiseReplacementCharge;

/// <summary>
/// Handles <see cref="RaiseReplacementChargeCommand"/>.
/// </summary>
/// <param name="accounts">The store holding the account.</param>
/// <param name="policy">The tariff.</param>
/// <param name="clock">The host's clock, which dates the charge.</param>
public sealed class RaiseReplacementChargeCommandHandler(
    IMemberAccountRepository accounts,
    ChargesPolicy policy,
    TimeProvider clock)
    : ICommandHandler<RaiseReplacementChargeCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RaiseReplacementChargeCommand command,
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

        return account.RaiseReplacementCharge(
            ChargeId.Generate(),
            LoanId.Create(command.LoanId),
            CopyId.Create(command.CopyId),
            clock.Today(),
            policy);
    }
}
