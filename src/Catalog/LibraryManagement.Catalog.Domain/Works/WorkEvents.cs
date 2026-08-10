using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Works;

/// <summary>
/// A work was cataloged.
/// </summary>
/// <param name="WorkId">The new work.</param>
/// <param name="PreferredTitle">The title it is filed under.</param>
public sealed record WorkRegistered(WorkId WorkId, Title PreferredTitle) : DomainEvent;

/// <summary>
/// A work is now recorded under a different title.
/// </summary>
/// <param name="WorkId">The work that changed.</param>
/// <param name="PreviousTitle">The title until now. It stops being findable.</param>
/// <param name="NewTitle">The title from now on.</param>
/// <remarks>
/// Both titles are carried so the search projection can replace one access point with the other.
/// Unlike a renamed author, a retitled work keeps no memory of its former title — the model records
/// no title variants — so this is a replacement, not a demotion; the day former titles must stay
/// findable, the model gains variants first and this event follows.
/// </remarks>
public sealed record WorkRetitled(WorkId WorkId, Title PreviousTitle, Title NewTitle) : DomainEvent;

/// <summary>
/// An author is now credited with a work.
/// </summary>
/// <param name="WorkId">The work.</param>
/// <param name="AuthorId">The author now credited.</param>
/// <remarks>
/// Nothing consumes this yet — the access-point index reads names off the author record, not the
/// credit — and it is published all the same, for the standing reason: an event not published when
/// it happened cannot be recovered afterwards. The work registered with its authors announces its
/// registration first, then one credit per author, so the stream never credits a work that does not
/// yet exist.
/// </remarks>
public sealed record WorkAuthorCredited(WorkId WorkId, AuthorId AuthorId) : DomainEvent;

/// <summary>
/// An author's credit was removed from a work.
/// </summary>
/// <param name="WorkId">The work.</param>
/// <param name="AuthorId">The author no longer credited.</param>
/// <remarks>
/// Raised only when there was a credit to remove: asking again for a state already reached is a
/// success that changes nothing, and an event saying otherwise would announce a removal that never
/// happened.
/// </remarks>
public sealed record WorkAuthorCreditRemoved(WorkId WorkId, AuthorId AuthorId) : DomainEvent;
