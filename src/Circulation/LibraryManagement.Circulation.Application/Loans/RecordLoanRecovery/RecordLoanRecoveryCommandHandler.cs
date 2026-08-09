using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Loans.RecordLoanRecovery;

/// <summary>
/// Handles <see cref="RecordLoanRecoveryCommand"/>.
/// </summary>
/// <param name="loans">The declared-lost loan the copy concerns, if any.</param>
/// <param name="policy">The circulation policy, which decides what a late day is.</param>
/// <param name="clock">The host's clock.</param>
/// <remarks>
/// Success when no declared loss ever involved the copy: a stocktake will one day find copies
/// whose loss no loan produced, and their recovery prices nothing here. Redelivery lands on the
/// aggregate's own once-only guard — a recovery already recorded announces nothing twice.
/// </remarks>
public sealed class RecordLoanRecoveryCommandHandler(
    ILoanRepository loans,
    CirculationPolicy policy,
    TimeProvider clock) : ICommandHandler<RecordLoanRecoveryCommand, Result>
{
    /// <inheritdoc/>
    public async ValueTask<Result> Handle(
        RecordLoanRecoveryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var copyId = CopyId.Create(command.CopyId);

        var loan = await loans.MostRecentlyDeclaredLostForCopyAsync(copyId, cancellationToken)
            .ConfigureAwait(false);

        if (loan is null)
        {
            return Result.Success();
        }

        return loan.RecordRecovery(clock.Today(), policy);
    }
}
