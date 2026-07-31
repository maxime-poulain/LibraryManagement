using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Works;

/// <summary>
/// A work was catalogued.
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
