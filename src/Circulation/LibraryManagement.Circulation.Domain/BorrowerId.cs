using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Circulation.Domain;

/// <summary>
/// Identifies a borrower — a member, seen as circulation sees them.
/// </summary>
/// <remarks>
/// <para>
/// Declared in this context and not borrowed from Members'. Referencing that module's domain would
/// bring the whole membership file with it, which is precisely what holding an identifier is meant
/// to avoid — Circulation names a person and never describes one.
/// </para>
/// <para>
/// It carries the <em>same</em> value as Members' own <c>MemberId</c> for the same person. Nothing
/// is mapped or looked up between the two: it is the model that the anticorruption layer
/// translates — <c>Member</c> becomes <c>Borrower</c>, and most of the member is dropped on the
/// way — never the identity. The glossary says it plainly so that nobody goes looking for a
/// correspondence table.
/// </para>
/// <para>
/// <see cref="EntityId{T}.Generate"/> is inherited and meaningless here: this context never mints
/// a person, it is handed one.
/// </para>
/// </remarks>
public sealed class BorrowerId : EntityId<BorrowerId>, IEntityId<BorrowerId>
{
    private BorrowerId(Guid value) : base(value)
    {
    }

    static BorrowerId IEntityId<BorrowerId>.FromValue(Guid value) => new(value);
}
