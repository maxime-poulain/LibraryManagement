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
/// The one question Circulation asks of Holdings: may this copy be lent?
/// </summary>
/// <remarks>
/// <para>
/// The published language of the Holdings → Circulation edge. It is answered synchronously, at the
/// desk, with a member standing there, which is why it is a port and not an event: a projection lag
/// would be visible to the person waiting.
/// </para>
/// <para>
/// <strong>It answers with lendability and never with availability.</strong> Whether the copy is
/// already out on loan is Circulation's own fact, and Circulation checks it immediately afterwards.
/// Holdings answering "available" would mean asserting something it cannot see, and would put half
/// of Circulation's rule in the wrong context.
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
    /// Answers whether a copy may be lent.
    /// </summary>
    /// <param name="copyId">The copy in hand, usually resolved from a barcode at the desk.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>What Holdings can say about it.</returns>
    /// <remarks>
    /// A <see cref="Guid"/> and not Holdings' own <c>CopyId</c>: that type belongs to the module's
    /// domain, and a published surface handing it out would make every consumer compile against the
    /// model this project exists to keep in.
    /// </remarks>
    ValueTask<Lendability> OfAsync(Guid copyId, CancellationToken cancellationToken = default);
}
