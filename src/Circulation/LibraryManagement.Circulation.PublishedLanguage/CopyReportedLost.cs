namespace LibraryManagement.Circulation.PublishedLanguage;

/// <summary>
/// Circulation gave up on a loan, so the copy it was for is unaccounted for.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence. Delivery across a module boundary is at-least-once, and this is
/// what a subscriber deduplicates by — so it is the mechanism's own requirement rather than
/// something the fact needs, and the day integration events travel in a real envelope — correlation,
/// causation, a schema version, all stamped by infrastructure — this is the field that moves onto it.
/// </param>
/// <param name="CopyId">The copy that never came back.</param>
/// <remarks>
/// <para>
/// <strong>A fact, not an instruction.</strong> It says what Circulation observed and never what
/// anyone should do about it: Holdings decides for itself that an unaccounted-for copy becomes
/// <c>Lost</c>, and it reaches the same conclusion from a stocktake that failed to find the copy.
/// Naming this <c>DeclareCopyLost</c> would have made it a command wearing an event's clothes, and
/// put a Holdings rule in Circulation's vocabulary.
/// </para>
/// <para>
/// <strong>Primitives only, and no base type.</strong> This project references nothing, so there is
/// nothing here to implement — and the identifier travels as a <c>Guid</c> rather than as a
/// <c>CopyId</c>, because a consumer compiling against Circulation's identifier types would be a
/// consumer that recompiles every time Circulation's domain moves. That is the coupling a published
/// language exists to prevent.
/// </para>
/// <para>
/// <strong>Nothing else crosses.</strong> The domain event behind it, <c>LoanDeclaredLost</c>,
/// carries the loan and the borrower too — Charges will price the replacement against one of them.
/// Neither appears here: Holdings has no use for either, and a contract that carried them would
/// invite Holdings to grow a use.
/// </para>
/// </remarks>
public sealed record CopyReportedLost(Guid EventId, Guid CopyId);
