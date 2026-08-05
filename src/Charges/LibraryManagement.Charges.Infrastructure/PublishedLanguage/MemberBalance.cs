using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Charges.Infrastructure.Persistence.Configurations;
using LibraryManagement.Circulation.PublishedLanguage;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Charges.Infrastructure.PublishedLanguage;

/// <summary>
/// Answers the one question Circulation asks of this module.
/// </summary>
/// <param name="context">The module's context, read from and never written through.</param>
/// <remarks>
/// <para>
/// <strong>The dependency-inverted edge, closed.</strong> Circulation declared this port in its own
/// published language for its own single use, and this is the implementation it waited for — the
/// anticorruption layer belongs to the downstream, so the contract was never Charges' to write.
/// Until now the composition answered <em>nobody owes anything</em>, which was the host's way of
/// saying the module did not exist.
/// </para>
/// <para>
/// <strong>An amount, never a verdict.</strong> What being owed forbids is Circulation's judgement,
/// made against its own policy. If this returned a boolean the rule would have moved into the wrong
/// context, and the vocabulary would have followed it.
/// </para>
/// <para>
/// <strong>No aggregate is materialized.</strong> This sits on the most frequent write path in the
/// system — every checkout and every hold — so it reads the charge rows directly. The netting is
/// done here rather than by the database because the amounts are value objects and a converted
/// property does not compose with a SQL aggregate; what comes back is two decimals per outstanding
/// charge, and what one person owes is a handful of rows by construction, since a charge that ends
/// leaves the account.
/// </para>
/// </remarks>
public sealed class MemberBalance(ChargesDbContext context) : IMemberBalance
{
    /// <inheritdoc/>
    public async ValueTask<decimal> OwedByAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var account = MemberId.Create(memberId);

        var outstanding = await context.Charges
            .Where(charge =>
                EF.Property<MemberId>(charge, MemberAccountConfiguration.AccountForeignKey) == account)
            .Select(charge => new { charge.Amount, charge.Paid })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return outstanding.Sum(charge => charge.Amount.Amount - charge.Paid.Amount);
    }
}
