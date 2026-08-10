using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Charges.Infrastructure.Persistence;

/// <summary>
/// The netting, in one place: every charge on an account, less what has been paid against it.
/// </summary>
/// <remarks>
/// <para>
/// Two callers ask this module the same figure for different reasons — the port Circulation
/// declared, so a checkout can be refused, and this module's own query, so a member's file can
/// show a number. They are properly separate contracts and were nearly two copies of one read.
/// This repository has already paid for that mistake once, in the provider call the five migrations
/// projects each spelled out, and the lesson recorded there applies unchanged: a shared thing
/// recognized one level too late is the ordinary shape of it.
/// </para>
/// <para>
/// <strong>No aggregate is materialized.</strong> One of the callers sits on the most frequent
/// write path in the system — every checkout and every hold — so this reads the charge rows
/// directly. The netting is done here rather than by the database because the amounts are value
/// objects and a converted property does not compose with a SQL aggregate; what comes back is two
/// decimals per outstanding charge, and what one person owes is a handful of rows by construction,
/// since a charge that ends leaves the account.
/// </para>
/// </remarks>
internal static class OutstandingCharges
{
    /// <summary>
    /// Sums what is still owed on one account.
    /// </summary>
    /// <param name="context">The module's store, read from and never written through.</param>
    /// <param name="memberId">The account to total.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The amount outstanding, and zero for an account that was never opened.</returns>
    internal static async ValueTask<decimal> OwedByAsync(
        ChargesDbContext context,
        MemberId memberId,
        CancellationToken cancellationToken)
    {
        var outstanding = await context.Charges
            .AsNoTracking()
            .Where(charge =>
                EF.Property<MemberId>(charge, MemberAccountConfiguration.AccountForeignKey) == memberId)
            .Select(charge => new { charge.Amount, charge.Paid })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return outstanding.Sum(charge => charge.Amount.Amount - charge.Paid.Amount);
    }
}
