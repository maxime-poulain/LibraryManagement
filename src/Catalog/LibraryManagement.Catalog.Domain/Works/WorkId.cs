using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Works;

/// <summary>
/// Identifies a <see cref="Work"/>.
/// </summary>
public sealed class WorkId : EntityId<WorkId>, IEntityId<WorkId>
{
    private WorkId(Guid value) : base(value)
    {
    }

    static WorkId IEntityId<WorkId>.FromValue(Guid value) => new(value);
}
