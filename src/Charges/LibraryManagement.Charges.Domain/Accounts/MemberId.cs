using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// Identifies the member an account belongs to.
/// </summary>
/// <remarks>
/// Redeclared here rather than borrowed, like every cross-context identifier in this solution: it
/// carries the same <see cref="Guid"/> Members issued and the same one Circulation holds as
/// <c>BorrowerId</c>, and no foreign key joins the three. The model is translated at each boundary;
/// the identity never is.
/// </remarks>
public sealed class MemberId : EntityId<MemberId>, IEntityId<MemberId>
{
    private MemberId(Guid value) : base(value)
    {
    }

    static MemberId IEntityId<MemberId>.FromValue(Guid value) => new(value);
}
