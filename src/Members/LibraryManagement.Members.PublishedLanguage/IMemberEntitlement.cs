namespace LibraryManagement.Members.PublishedLanguage;

/// <summary>
/// What Members can say about whether a person may borrow.
/// </summary>
/// <remarks>
/// <strong>Three answers and not two.</strong> A member nobody has heard of is not a lapsed one —
/// an unknown card means a mis-scan or a card from another library, where a lapsed membership
/// opens a renewal conversation, and a librarian does different things with the two. Folding them
/// together would throw away the one distinction the person at the desk has to act on — the same
/// three-way shape the lendability answer takes, for the same reason.
/// </remarks>
public enum Entitlement
{
    /// <summary>The membership currently holds. The category says as what.</summary>
    Entitled,

    /// <summary>The member exists and their membership has lapsed.</summary>
    Lapsed,

    /// <summary>Nobody is enrolled under that identifier.</summary>
    NoSuchMember,
}

/// <summary>
/// The category, as the published language speaks it.
/// </summary>
/// <remarks>
/// The same three names as the module's own enumeration, translated by value and never
/// referenced: a published surface handing out the domain's type would make every consumer
/// compile against the model this project exists to keep in. On the far side, Circulation's
/// anticorruption layer folds this into its own <c>Borrower</c>.
/// </remarks>
public enum MemberCategory
{
    /// <summary>An adult member.</summary>
    Adult,

    /// <summary>A minor.</summary>
    Child,

    /// <summary>A student.</summary>
    Student,
}

/// <summary>
/// The answer to the one question: whether the membership holds, and as what.
/// </summary>
/// <param name="Entitlement">What Members can say about the person.</param>
/// <param name="Category">The category, carried only when the answer grants something. A lapsed
/// member's category is decided afresh at renewal, not reported stale here.</param>
public sealed record EntitlementAnswer(Entitlement Entitlement, MemberCategory? Category);

/// <summary>
/// The one question Circulation asks of Members: may this person borrow, and as what?
/// </summary>
/// <remarks>
/// <para>
/// The published language of the Members → Circulation edge. It is answered synchronously, at the
/// desk, with the member standing there, which is why it is a port and not an event: a projection
/// lag would be visible to the person waiting.
/// </para>
/// <para>
/// <strong>It answers with entitlement and never with standing.</strong> Whether a debt forbids
/// the loan is Circulation's judgement over an amount Charges states; whether the borrower is at
/// the cap is Circulation's own count. Members answering "may borrow" would fold both into the
/// wrong context — it answers for the subscription, the only thing it can vouch for.
/// </para>
/// <para>
/// A port rather than a query, deliberately, for the reason the lendability port gives: the
/// consumer is a command handler on Circulation's write path, and a command handler may not
/// depend on the query dispatcher — an architecture rule enforces it.
/// </para>
/// </remarks>
public interface IMemberEntitlement
{
    /// <summary>
    /// Answers whether a person may borrow, and as what.
    /// </summary>
    /// <param name="memberId">The member in hand, usually resolved from a card at the desk.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>What Members can say about them.</returns>
    /// <remarks>
    /// A <see cref="Guid"/> and not Members' own <c>MemberId</c>: that type belongs to the
    /// module's domain, and a published surface handing it out would make every consumer compile
    /// against the model this project exists to keep in. The value is the same one Circulation
    /// holds as <c>BorrowerId</c> — the boundary translates the model, never the identity.
    /// </remarks>
    ValueTask<EntitlementAnswer> OfAsync(Guid memberId, CancellationToken cancellationToken = default);
}
