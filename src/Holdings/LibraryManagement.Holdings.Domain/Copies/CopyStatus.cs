namespace LibraryManagement.Holdings.Domain.Copies;

/// <summary>
/// What Holdings knows about a copy's disposition. The whole set, and it is enumerated in the
/// Holdings glossary of the strategic design and nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// <strong>A status is not availability.</strong> Availability is the conjunction of a Holdings fact
/// and a Circulation fact — whether the copy is out on loan — computed by whoever asks and stored in
/// neither context.
/// </para>
/// <para>
/// Which is why <see cref="InService"/> is not called <c>OnShelf</c>. A copy someone borrowed last
/// Tuesday is not on a shelf, and this context has no way to know that it isn't: a value naming a
/// physical location would be false for a large share of the stock at any moment, and silently so.
/// </para>
/// </remarks>
public enum CopyStatus
{
    /// <summary>
    /// Nothing about this copy prevents it from being lent, as far as Holdings can tell.
    /// </summary>
    InService,

    /// <summary>Temporarily out of the lendable stock, and expected back in it.</summary>
    InRepair,

    /// <summary>Held, consultable on site, never lent.</summary>
    ReferenceOnly,

    /// <summary>
    /// Removed from the collection on purpose — weeded. Terminal: a withdrawal is recorded because
    /// it happened, and a copy that came back would be an accession rather than an undo.
    /// </summary>
    Withdrawn,

    /// <summary>
    /// Unaccounted for. Distinct from <see cref="Withdrawn"/> because nobody decided it, and not
    /// terminal for the same reason: copies turn up.
    /// </summary>
    Lost,
}
