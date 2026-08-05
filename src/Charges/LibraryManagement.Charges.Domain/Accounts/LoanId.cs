using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// Identifies the loan a charge prices.
/// </summary>
/// <remarks>
/// What a charge is <em>for</em>, and the only thing a member disputing one at the desk wants named.
/// It arrives from Circulation and is stored, never resolved: this context cannot load a loan and
/// has no reason to.
/// </remarks>
public sealed class LoanId : EntityId<LoanId>, IEntityId<LoanId>
{
    private LoanId(Guid value) : base(value)
    {
    }

    static LoanId IEntityId<LoanId>.FromValue(Guid value) => new(value);
}
