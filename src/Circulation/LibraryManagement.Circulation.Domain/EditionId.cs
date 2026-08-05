using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Circulation.Domain;

/// <summary>
/// Identifies the edition a hold queue serves and a loan's copy belongs to. Catalog's edition,
/// named here.
/// </summary>
/// <remarks>
/// A hold is a claim on an edition, never on a copy — a borrower waiting for <em>Le Petit
/// Prince</em> wants the next copy, not a particular volume — so the edition is the identity a
/// queue keys on. The value is the same one Catalog holds; the boundary translates the model,
/// never the identity.
/// </remarks>
public sealed class EditionId : EntityId<EditionId>, IEntityId<EditionId>
{
    private EditionId(Guid value) : base(value)
    {
    }

    static EditionId IEntityId<EditionId>.FromValue(Guid value) => new(value);
}
