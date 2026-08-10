using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.Persistence;
using LibraryManagement.Members.PublishedLanguage;
using Microsoft.EntityFrameworkCore;
using DomainCategory = LibraryManagement.Members.Domain.Members.MemberCategory;
using PublishedCategory = LibraryManagement.Members.PublishedLanguage.MemberCategory;

namespace LibraryManagement.Members.Infrastructure.PublishedLanguage;

/// <summary>
/// Answers <see cref="IMemberEntitlement"/> from the module's own store.
/// </summary>
/// <param name="context">The module's store.</param>
/// <param name="clock">The host's clock. Entitlement is computed against the day of the asking,
/// never stored — a stored status would be false from midnight until a job corrected it.</param>
/// <remarks>
/// It reads the table rather than loading the aggregate: the question wants one row and three
/// columns, and <c>Member.MembershipIsCurrentOn</c> is the same rule written where the domain can
/// state it. The two are kept in step by a test rather than by a comment, because a projection of
/// a rule that drifts from the rule is worse than no projection.
/// </remarks>
public sealed class MemberEntitlement(MembersDbContext context, TimeProvider clock)
    : IMemberEntitlement
{
    /// <inheritdoc/>
    public async ValueTask<EntitlementAnswer> OfAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        // Built into the module's own identifier first: the key carries a value conversion, so a
        // comparison on `Id.Value` does not translate — the provider sees a member access on a
        // type it has no column for.
        var id = MemberId.Create(memberId);

        var membership = await context.Members
            .Where(member => member.Id == id)
            .Select(member => new
            {
                member.Category,
                member.MembershipStart,
                member.MembershipEnd,
                member.ErasedOn,
                member.MergedInto,
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (membership is null || membership.ErasedOn is not null || membership.MergedInto is not null)
        {
            // An erased record answers exactly as an absent one: the identifier no longer
            // resolves to a person, and that is what the erasure promised.
            //
            // A merged one joins them, and this single clause is what keeps new loans and holds off
            // an absorbed identifier: Circulation asks this question before every checkout and every
            // hold, so a card from the record a member of staff merged away stops working the
            // moment they say so. No consumer had to learn anything.
            return new EntitlementAnswer(Entitlement.NoSuchMember, Category: null);
        }

        // The UTC day, the only one the system has — the same reading the Application layer's
        // clock extension records the reasons for.
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        return today >= membership.MembershipStart && today <= membership.MembershipEnd
            ? new EntitlementAnswer(Entitlement.Entitled, Translate(membership.Category))
            : new EntitlementAnswer(Entitlement.Lapsed, Category: null);
    }

    // By value, never by reference: the published enum carries the same three names, and this
    // switch is the whole translation. A new category fails it loudly here rather than silently
    // downstream.
    private static PublishedCategory Translate(DomainCategory category) => category switch
    {
        DomainCategory.Adult => PublishedCategory.Adult,
        DomainCategory.Child => PublishedCategory.Child,
        DomainCategory.Student => PublishedCategory.Student,
        _ => throw new InvalidOperationException(
            $"The registry holds a category '{category}' the published language cannot speak."),
    };
}
