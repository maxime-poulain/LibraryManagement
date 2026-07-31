using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Holdings.Domain.Copies;

/// <summary>
/// Identifies the edition a <see cref="Copy"/> is a copy of. Catalog's edition, named here.
/// </summary>
/// <remarks>
/// <para>
/// Declared in this context and not borrowed from Catalog's. Referencing that module's domain would
/// bring the whole catalog with it, which is precisely what holding an identifier is meant to avoid
/// — Holdings names an edition and never describes one.
/// </para>
/// <para>
/// It carries the <em>same</em> value as Catalog's own identifier for the same edition. Nothing is
/// mapped or looked up between the two: it is the model that a boundary translates, never the
/// identity. The glossary records the same arrangement for <c>BorrowerId</c> and <c>MemberId</c>,
/// and for the same reason — so that nobody goes looking for a correspondence table.
/// </para>
/// <para>
/// <see cref="EntityId{T}.Generate"/> is inherited and meaningless here. This context never mints an
/// edition; it is handed one, so <see cref="EntityId{T}.Create"/> is the only way one is ever built.
/// </para>
/// </remarks>
public sealed class EditionId : EntityId<EditionId>, IEntityId<EditionId>
{
    private EditionId(Guid value) : base(value)
    {
    }

    static EditionId IEntityId<EditionId>.FromValue(Guid value) => new(value);
}
