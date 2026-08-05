using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Circulation.Domain.Holds;

/// <summary>
/// Identifies a <see cref="Hold"/> within its queue's life, and in the history projection after
/// it leaves.
/// </summary>
public sealed class HoldId : EntityId<HoldId>, IEntityId<HoldId>
{
    private HoldId(Guid value) : base(value)
    {
    }

    static HoldId IEntityId<HoldId>.FromValue(Guid value) => new(value);
}
