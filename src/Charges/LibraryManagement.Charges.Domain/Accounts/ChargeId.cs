using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// Identifies one charge within an account.
/// </summary>
public sealed class ChargeId : EntityId<ChargeId>, IEntityId<ChargeId>
{
    private ChargeId(Guid value) : base(value)
    {
    }

    static ChargeId IEntityId<ChargeId>.FromValue(Guid value) => new(value);
}
