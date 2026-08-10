using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Editions;

/// <summary>
/// One published form of a <see cref="Works.Work"/> — the Gallimard 2015 paperback, as opposed to
/// the intellectual creation it prints.
/// </summary>
/// <remarks>
/// <para>
/// An aggregate root of its own, holding a <see cref="Works.WorkId"/> — this settles the question
/// the strategic design left open, and the hold model is what settled it: reservation queues key on
/// an edition, so an edition must be independently addressable whichever way the modeling fell.
/// A work knows nothing of its editions, exactly as an author knows nothing of the works credited
/// to them; "the editions of this work" is a query, not a navigation.
/// </para>
/// <para>
/// Deliberately thin: an identity, the work it prints, and the ISBN it bears when it bears one.
/// No ISBN is not an oversight — grey literature, self-published works and everything printed
/// before 1970 carry none, and those are exactly what the manual commands exist to catalog. The
/// publisher, the format and a translation's contributors arrive with their own concepts the day
/// they are modeled; a thin edition is what lets Holdings attach copies without waiting for them.
/// </para>
/// </remarks>
public sealed class Edition : AggregateRoot<EditionId>
{
    private Edition(EditionId id, WorkId workId) : base(id) => WorkId = workId;

    /// <summary>Gets the work this edition prints. Identity only, never the work itself.</summary>
    public WorkId WorkId { get; }

    /// <summary>Gets the ISBN the edition bears, or <see langword="null"/> when it bears none.</summary>
    public Isbn? Isbn { get; private set; }

    /// <summary>
    /// Gets the edition this record was merged into, or <see langword="null"/> while it is still a
    /// record in its own right.
    /// </summary>
    /// <remarks>
    /// Terminal, the way erasure is for a member: an absorbed edition acts no more, and every
    /// operation on it refuses. The row stays and keeps its identifier, because downstream contexts
    /// held that identifier and a deleted record would orphan them — the same reason Members keeps
    /// an erased member's key. What changes is that the identifier now resolves to a pointer rather
    /// than to a record: whoever still holds it can follow it exactly once, to the survivor.
    /// </remarks>
    public EditionId? AbsorbedInto { get; private set; }

    /// <summary>
    /// Catalogs an edition of a work.
    /// </summary>
    /// <param name="id">The identifier the edition will keep for its whole life.</param>
    /// <param name="workId">The work this edition prints. That it exists is the caller's rule to enforce.</param>
    /// <param name="isbn">The ISBN the edition bears, or <see langword="null"/> when it bears none.</param>
    /// <returns>The new edition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> or <paramref name="workId"/> is null.</exception>
    /// <remarks>
    /// Returns an <see cref="Edition"/> and not a result, for the reason
    /// <see cref="Authors.Author.Register"/> gives: every rule that could refuse one has already
    /// been enforced by the value objects, and whether the work exists spans two aggregates — the
    /// handler asks that question, exactly as crediting an author does.
    /// </remarks>
    public static Edition Register(EditionId id, WorkId workId, Isbn? isbn)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(workId);

        var edition = new Edition(id, workId)
        {
            Isbn = isbn,
        };

        edition.AddDomainEvent(new EditionRegistered(id, workId, isbn));

        return edition;
    }

    /// <summary>
    /// Records that this edition was merged into another, and announces it.
    /// </summary>
    /// <param name="survivingEditionId">The edition that goes on answering.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="survivingEditionId"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// <strong>Deliberately unguarded, and deliberately <c>internal</c>.</strong> Whether two
    /// records may be joined is decided by <see cref="EditionMergeDomainService"/>, which holds all
    /// four rules together — a merge is one question, and rules that answer one question drift
    /// apart when they are kept in two places. This method is what remains once the decision is
    /// made: the state change and the fact.
    /// </para>
    /// <para>
    /// <c>internal</c> is what makes that arrangement hold rather than merely describe it. The
    /// application layer cannot see this method, so the service is not one path among two — it is
    /// the only one. The module's own tests do see it, which is what lets the surface stay this
    /// narrow instead of widening to become testable.
    /// </para>
    /// <para>
    /// The direction is the caller's to choose and nothing here second-guesses it. Which of two
    /// duplicate records deserves to survive is a cataloging judgement about which is better
    /// described, and nothing in the model can tell.
    /// </para>
    /// </remarks>
    internal void AbsorbInto(EditionId survivingEditionId)
    {
        ArgumentNullException.ThrowIfNull(survivingEditionId);

        AbsorbedInto = survivingEditionId;

        AddDomainEvent(new EditionsMerged(Id, survivingEditionId));
    }
}
