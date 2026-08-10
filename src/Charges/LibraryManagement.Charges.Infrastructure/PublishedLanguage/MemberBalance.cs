using LibraryManagement.Charges.Domain.Accounts;
using LibraryManagement.Charges.Infrastructure.Persistence;
using LibraryManagement.Circulation.PublishedLanguage;

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
/// <strong>No aggregate is materialized</strong>, and the read itself lives in
/// <see cref="OutstandingCharges"/> rather than here: this module's own query answers the same
/// figure for a different audience, and one netting spelled twice is the mistake the migrations
/// projects already taught this repository to see coming.
/// </para>
/// </remarks>
public sealed class MemberBalance(ChargesDbContext context) : IMemberBalance
{
    /// <inheritdoc/>
    public ValueTask<decimal> OwedByAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
        => OutstandingCharges.OwedByAsync(context, MemberId.Create(memberId), cancellationToken);
}
