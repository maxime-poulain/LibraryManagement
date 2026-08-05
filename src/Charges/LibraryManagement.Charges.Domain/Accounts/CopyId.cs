using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// Identifies the copy behind the loan a charge prices.
/// </summary>
/// <remarks>
/// Recorded beside the loan and not instead of it, and the pair is deliberate. A charge is
/// <em>created</em> by a fact from Circulation, which speaks loans; a replacement charge is
/// <em>undone</em> by a fact from Holdings, which speaks copies and has never heard of a loan.
/// Without this, an arriving reversal could not find the charge it concerns — not awkwardly, but at
/// all.
/// </remarks>
public sealed class CopyId : EntityId<CopyId>, IEntityId<CopyId>
{
    private CopyId(Guid value) : base(value)
    {
    }

    static CopyId IEntityId<CopyId>.FromValue(Guid value) => new(value);
}
