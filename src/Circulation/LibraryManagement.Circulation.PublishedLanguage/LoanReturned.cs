namespace LibraryManagement.Circulation.PublishedLanguage;

/// <summary>
/// A copy came back and its loan closed — punctual and late returns alike.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery.
/// </param>
/// <param name="BorrowerId">Who had it. The same value Members issued for the person.</param>
/// <param name="LoanId">The loan that closed.</param>
/// <param name="CopyId">The copy that came back.</param>
/// <param name="DaysLate">
/// How late, as this context counted it: open days of the library's calendar, never the closed
/// ones — a day nobody could return is a day nobody is billed for, and the calendar that decides
/// which days those are stays on this side of the boundary. Zero is possible and is announced all
/// the same — whether there is anything to charge for it is not a circulation question.
/// </param>
/// <remarks>
/// <para>
/// <strong>Days, never a price.</strong> Whether a return was late is a circulation fact; what
/// lateness costs is a money question, and the context that answers it owns the tariff. A price
/// here would have moved the tariff into lending, where an amnesty would become a deployment.
/// </para>
/// <para>
/// The contract bears the same name as the fact behind it, and it earned that name back: it was
/// <c>LoanReturnedLate</c>, published for every return, punctual ones included — a name asserting
/// a judgement that was false for most deliveries, and a trap for any future subscriber reading
/// it as a filter. A contract names the fact; what the fact means is each reader's own business.
/// </para>
/// <para>
/// The copy travels beside the loan because the context that prices this records both: what it
/// charges for is undone, one day, by a fact that speaks copies.
/// </para>
/// </remarks>
public sealed record LoanReturned(
    Guid EventId,
    Guid BorrowerId,
    Guid LoanId,
    Guid CopyId,
    int DaysLate);

/// <summary>
/// A loan ended without its copy coming back: the library stopped waiting.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery.
/// </param>
/// <param name="BorrowerId">Who had it.</param>
/// <param name="LoanId">The loan that ended.</param>
/// <param name="CopyId">The copy that never came back.</param>
/// <remarks>
/// <para>
/// The same domain event that tells Holdings a copy is unaccounted for tells this contract's
/// reader that somebody may owe for it, and the two contracts carry different things: Holdings
/// has no use for a borrower. One fact, two audiences, two flattenings — which is what keeps
/// either consumer from growing a use for what the other needed.
/// </para>
/// <para>
/// It was <c>LoanWrittenOff</c>, and the rename is the <c>CopyReportedLost</c> discipline applied
/// a second time: a write-off is the reader's ledger word, and a fact leaving this context must
/// not name its consequence in another context's vocabulary. What this context knows is that the
/// loan ended and no copy came back; what that costs is the reader's decision.
/// </para>
/// </remarks>
public sealed record LoanEndedUnreturned(
    Guid EventId,
    Guid BorrowerId,
    Guid LoanId,
    Guid CopyId);
