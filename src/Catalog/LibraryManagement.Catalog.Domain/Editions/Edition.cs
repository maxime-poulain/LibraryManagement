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
/// an edition, so an edition must be independently addressable whichever way the modelling fell.
/// A work knows nothing of its editions, exactly as an author knows nothing of the works credited
/// to them; "the editions of this work" is a query, not a navigation.
/// </para>
/// <para>
/// Deliberately thin: an identity, the work it prints, and the ISBN it bears when it bears one.
/// No ISBN is not an oversight — grey literature, self-published works and everything printed
/// before 1970 carry none, and those are exactly what the manual commands exist to catalogue. The
/// publisher, the format and a translation's contributors arrive with their own concepts the day
/// they are modelled; a thin edition is what lets Holdings attach copies without waiting for them.
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
    /// Catalogues an edition of a work.
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
}
