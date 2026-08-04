using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Members.Domain.Members;

/// <summary>
/// Identifies a <see cref="Member"/>.
/// </summary>
/// <remarks>
/// The identity, and the card number is not: a card is replaced whenever one is lost or stops
/// working, and a member whose identity moved with their card would take their whole loan history
/// with them. It also carries the <em>same</em> value Circulation will hold as <c>BorrowerId</c> —
/// the anticorruption layer on that edge translates the model, never the identity, so nobody
/// should go looking for a correspondence table.
/// </remarks>
public sealed class MemberId : EntityId<MemberId>, IEntityId<MemberId>
{
    private MemberId(Guid value) : base(value)
    {
    }

    static MemberId IEntityId<MemberId>.FromValue(Guid value) => new(value);
}
