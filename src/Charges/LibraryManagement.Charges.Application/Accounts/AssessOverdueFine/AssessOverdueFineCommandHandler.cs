using LibraryManagement.Charges.Domain;
using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.AssessOverdueFine;

/// <summary>
/// Handles <see cref="AssessOverdueFineCommand"/>.
/// </summary>
/// <param name="accounts">The store holding the account.</param>
/// <param name="policy">The tariff.</param>
/// <param name="clock">The host's clock, which dates the charge.</param>
/// <remarks>
/// <strong>The first charge opens the account.</strong> There is no enrollment to react to and no
/// moment anyone performs: an account per enrolled member would be a row forever answering a
/// question nobody asks, so it comes into being the first time a member is charged. Which is also
/// why nothing here fails when the account is missing.
/// </remarks>
public sealed class AssessOverdueFineCommandHandler(
    IMemberAccountRepository accounts,
    ChargesPolicy policy,
    TimeProvider clock)
    : ICommandHandler<AssessOverdueFineCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        AssessOverdueFineCommand command,
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

        return account.AssessOverdueFine(
            ChargeId.Generate(),
            LoanId.Create(command.LoanId),
            CopyId.Create(command.CopyId),
            command.DaysLate,
            clock.Today(),
            policy);
    }
}
