namespace LibraryManagement.Holdings.PublishedLanguage;

/// <summary>
/// What Holdings can say about whether a copy may be lent.
/// </summary>
/// <remarks>
/// <strong>Three answers and not two.</strong> A copy nobody has heard of is not a refusal — it is a
/// mis-scan, or a copy never accessioned, and a librarian handles either differently from a copy
/// that is simply in repair. Folding it into <see cref="NotLendable"/> would throw away the one
/// distinction the person at the desk has to act on.
///
/// Named for what it answers rather than for what asks it, so that the implementation may take the
/// obvious name: <c>CopyLendability</c> is the thing that answers, this is the answer.
/// </remarks>
public enum Lendability
{
    /// <summary>Nothing in Holdings prevents this copy from being lent.</summary>
    Lendable,

    /// <summary>The copy exists, and its status forbids lending it.</summary>
    NotLendable,

    /// <summary>No copy is held under that identifier.</summary>
    NoSuchCopy,
}

/// <summary>
/// The answer about the copy in hand: whether it may be lent, and which edition it is a copy of.
/// </summary>
/// <param name="Lendability">What Holdings can say about lending it.</param>
/// <param name="EditionId">The edition the copy belongs to, or <see langword="null"/> when no copy
/// is held under the identifier. Carried because the asker's next question is almost always about
/// the edition — which hold queue this copy feeds — and a desk scan should cost one round trip,
/// not two.</param>
public sealed record LendabilityAnswer(Lendability Lendability, Guid? EditionId);

/// <summary>
/// The questions Circulation asks of Holdings: may this copy be lent, and which copies of an
/// edition could be.
/// </summary>
/// <remarks>
/// <para>
/// The published language of the Holdings → Circulation edge. It is answered synchronously, at the
/// desk, with a member standing there, which is why it is a port and not an event: a projection
/// lag would be visible to the person waiting.
/// </para>
/// <para>
/// <strong>It answers with lendability and never with availability.</strong> Whether a copy is
/// already out on loan is Circulation's own fact, and Circulation checks it itself. Holdings
/// answering "available" would mean asserting something it cannot see, and would put half of
/// Circulation's rule in the wrong context. The same division holds for
/// <see cref="LendableCopiesOfAsync"/>: it lists the copies whose status permits lending, and
/// which of them are out, or set aside for a hold, is the asker's own arithmetic.
/// </para>
/// <para>
/// The surface began as the single question and grew when Circulation was built — the context map
/// makes this edge Customer/Supplier precisely so a downstream that needs more of a contract can
/// have it changed, by saying so, rather than working around it.
/// </para>
/// <para>
/// A port rather than a query, deliberately. The consumer is a command handler on Circulation's
/// write path, and a command handler may not depend on the query dispatcher — an architecture rule
/// enforces it, because a command borrowing the read side turns a cross-module read into a write no
/// module's invariants cover. A query would leave Circulation the choice between breaking that rule
/// and asking the same question twice.
/// </para>
/// </remarks>
public interface ICopyLendability
{
    /// <summary>
    /// Answers whether a copy may be lent, and which edition it belongs to.
    /// </summary>
    /// <param name="copyId">The copy in hand, usually resolved from a barcode at the desk.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>What Holdings can say about it.</returns>
    /// <remarks>
    /// <see cref="Guid"/>s and not Holdings' own identifiers: those types belong to the module's
    /// domain, and a published surface handing them out would make every consumer compile against
    /// the model this project exists to keep in.
    /// </remarks>
    ValueTask<LendabilityAnswer> OfAsync(Guid copyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the copies of an edition whose status permits lending.
    /// </summary>
    /// <param name="editionId">The edition in question.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The identifiers of the copies Holdings would lend, possibly none.</returns>
    /// <remarks>
    /// What a hold refusal needs: a hold may not be placed while a copy is available on the shelf
    /// — that would be a checkout — and availability is this list minus the copies Circulation
    /// itself knows to be out or set aside.
    /// </remarks>
    ValueTask<IReadOnlyList<Guid>> LendableCopiesOfAsync(
        Guid editionId,
        CancellationToken cancellationToken = default);
}
