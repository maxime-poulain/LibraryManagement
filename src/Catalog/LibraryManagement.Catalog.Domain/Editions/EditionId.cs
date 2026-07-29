using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Editions;

/// <summary>
/// Identifies an <see cref="Edition"/>.
/// </summary>
public sealed class EditionId : EntityId<EditionId>, IEntityId<EditionId>
{
    private EditionId(Guid value) : base(value)
    {
    }

    static EditionId IEntityId<EditionId>.FromValue(Guid value) => new(value);
}
