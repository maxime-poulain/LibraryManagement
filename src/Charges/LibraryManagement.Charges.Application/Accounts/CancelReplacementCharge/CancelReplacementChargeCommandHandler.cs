using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Charges.Application.Accounts.CancelReplacementCharge;

/// <summary>
/// Handles <see cref="CancelReplacementChargeCommand"/>.
/// </summary>
/// <param name="accounts">The account holding something outstanding for the copy, if any.</param>
/// <remarks>
/// Success when no account holds anything outstanding for the copy — the charge was paid, was
/// already cancelled, or never existed, and each of those is an answer rather than a failure.
/// This arrives from another module's drain, and the aggregate's own cancellation answers
/// success for a copy with nothing outstanding, which is what makes redelivery safe.
/// </remarks>
public sealed class CancelReplacementChargeCommandHandler(IMemberAccountRepository accounts)
    : ICommandHandler<CancelReplacementChargeCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        CancelReplacementChargeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var copyId = CopyId.Create(command.CopyId);

        var account = await accounts.GetByOutstandingReplacementForAsync(copyId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return Result.Success();
        }

        return account.CancelReplacementChargeFor(copyId);
    }
}
