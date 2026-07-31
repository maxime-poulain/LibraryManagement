namespace LibraryManagement.Catalog.Application.Works.GetWorkById;

/// <summary>
/// A work as a reader of the catalogue sees it.
/// </summary>
/// <param name="WorkId">The work.</param>
/// <param name="Title">Its title.</param>
/// <param name="Authors">The authors credited, under the names the catalogue files them by.</param>
/// <remarks>
/// <para>
/// Carries primitives and not the domain's own types. This leaves the context — it is what a caller
/// receives — and shipping <c>Title</c> or <c>AuthorId</c> across would make every consumer compile
/// against the catalogue's model, which is precisely what a bounded context refuses.
/// </para>
/// <para>
/// The <c>Dto</c> suffix is the convention for exactly that, and an architecture test enforces it on
/// every query's answer. It is not decoration: the suffix tells a reader, at the point of use, that
/// this type may be reshaped for whoever consumes it and owes nothing to the model behind it. A
/// query answering with a domain type would compile perfectly well and quietly export the aggregate.
/// </para>
/// </remarks>
public sealed record WorkDetailsDto(
    Guid WorkId,
    string Title,
    IReadOnlyList<CreditedAuthorDto> Authors);

/// <summary>
/// One author credited with a work.
/// </summary>
/// <param name="AuthorId">The author.</param>
/// <param name="PreferredName">The name the catalogue files them under, not any variant.</param>
/// <remarks>
/// Suffixed too, though the rule only reaches the answer's own type. It travels just as far and is
/// just as free of the domain, and a <c>CreditedAuthor</c> sitting inside a <c>WorkDetailsDto</c>
/// would read as a different kind of thing when it is not.
/// </remarks>
public sealed record CreditedAuthorDto(Guid AuthorId, string PreferredName);
