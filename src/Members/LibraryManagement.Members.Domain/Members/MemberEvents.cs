using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Members.Domain.Members;

// Members publishes facts about people and their subscriptions. It consumes nothing: no context in
// the map is upstream of it. The one thing Circulation needs from it is a question — may this
// person borrow, and as what? — answered synchronously through the module's published language,
// never announced; and Notifications reaches an address by query, not by subscribing here.
//
// Every event feeds a projection and nothing else, today. That is not a reason to withhold them:
// the member file staff consult is a read model fed by exactly this stream, and an event not
// published when it happened cannot be recovered afterwards.

/// <summary>
/// A person became a member.
/// </summary>
/// <param name="MemberId">The new member.</param>
/// <param name="Category">The category the librarian decided.</param>
/// <param name="CardNumber">The card issued at the desk.</param>
public sealed record MemberEnrolled(
    MemberId MemberId,
    MemberCategory Category,
    CardNumber CardNumber) : DomainEvent;

/// <summary>
/// A membership was renewed.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="NewEnd">The last day of the renewed period, inclusive.</param>
public sealed record MembershipRenewed(MemberId MemberId, DateOnly NewEnd) : DomainEvent;

/// <summary>
/// A member was moved to another category.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="PreviousCategory">What they were enrolled as before.</param>
/// <param name="NewCategory">What they are enrolled as now.</param>
public sealed record MemberCategoryChanged(
    MemberId MemberId,
    MemberCategory PreviousCategory,
    MemberCategory NewCategory) : DomainEvent;

/// <summary>
/// A member's card was replaced.
/// </summary>
/// <param name="MemberId">The member. Their identity did not change, which is the point.</param>
/// <param name="PreviousCardNumber">The number that is gone. A found card presented by someone
/// else must point at nothing, so a projection keyed on it has to retract it.</param>
/// <param name="NewCardNumber">The number from now on.</param>
public sealed record CardReplaced(
    MemberId MemberId,
    CardNumber PreviousCardNumber,
    CardNumber NewCardNumber) : DomainEvent;

/// <summary>
/// The ways of reaching a member changed.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="NewContactDetails">The channels from now on, possibly none.</param>
public sealed record ContactDetailsChanged(
    MemberId MemberId,
    ContactDetails NewContactDetails) : DomainEvent;

/// <summary>
/// Who a member is reached through changed.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="NewGuardian">The guardian from now on, or <see langword="null"/> when the member
/// is now reached directly.</param>
public sealed record GuardianChanged(MemberId MemberId, Guardian? NewGuardian) : DomainEvent;

/// <summary>
/// A member's name changed — the marriage and the typo alike, one event for both.
/// </summary>
/// <param name="MemberId">The member.</param>
/// <param name="PreviousName">The name that no longer applies.</param>
/// <param name="NewName">The name from now on.</param>
public sealed record MemberRenamed(
    MemberId MemberId,
    MemberName PreviousName,
    MemberName NewName) : DomainEvent;

/// <summary>
/// A member's record was emptied at their request; the identifier stands, resolving to nobody.
/// </summary>
/// <param name="MemberId">The identifier that remains.</param>
/// <remarks>
/// The identifier and nothing else, deliberately: an event announcing an erasure must not itself
/// carry what was erased, and the read models that consume this owe the same emptying to their
/// own copies.
/// </remarks>
public sealed record MemberErased(MemberId MemberId) : DomainEvent;
