using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Charges.Infrastructure.Persistence;

/// <summary>
/// Finds and tracks accounts in the module's store.
/// </summary>
/// <param name="context">The module's context.</param>
/// <remarks>
/// It never saves. <c>UnitOfWorkBehavior</c> writes once, on success, which is what makes a command
/// the unit of consistency rather than a repository call.
/// </remarks>
public sealed class MemberAccountRepository(ChargesDbContext context) : IMemberAccountRepository
{
    /// <inheritdoc/>
    public async Task<MemberAccount?> GetByMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberId);

        // The charges come with the account, always: every rule the aggregate holds reads them, and
        // an account loaded without them would let a decision be made against a state that was
        // never true.
        return await context.Accounts
            .Include(account => account.Charges)
            .SingleOrDefaultAsync(account => account.Id == memberId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<MemberAccount?> GetByOutstandingReplacementForAsync(
        CopyId copyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(copyId);

        // Reached from a fact that names a copy and nothing else — no member, no loan — which is
        // the one lookup here that does not start from an identity. The filtered index on the copy
        // is what keeps it from being a scan.
        var memberId = await context.Charges
            .OfType<ReplacementCharge>()
            .Where(charge => charge.CopyId == copyId)
            .Select(charge => EF.Property<MemberId>(charge, MemberAccountConfiguration.AccountForeignKey))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return memberId is null
            ? null
            : await GetByMemberAsync(memberId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Add(MemberAccount account) => context.Accounts.Add(account);
}
