namespace LibraryManagement.Circulation.PublishedLanguage;

/// <summary>
/// A copy came back after its due date.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery.
/// </param>
/// <param name="BorrowerId">Who had it. The same value Members issued for the person.</param>
/// <param name="LoanId">The loan that closed.</param>
/// <param name="CopyId">The copy that came back.</param>
/// <param name="DaysLate">
/// How late, as this context counted it. Zero is possible and is announced all the same — whether
/// there is anything to charge for it is not a circulation question.
/// </param>
/// <remarks>
/// <para>
/// <strong>Days, never a price.</strong> Whether a return was late is a circulation fact; what
/// lateness costs is a money question, and the context that answers it owns the tariff. A price
/// here would have moved the tariff into lending, where an amnesty would become a deployment.
/// </para>
/// <para>
/// The copy travels beside the loan because the context that prices this records both: what it
/// charges for is undone, one day, by a fact that speaks copies.
/// </para>
/// </remarks>
public sealed record LoanReturnedLate(
    Guid EventId,
    Guid BorrowerId,
    Guid LoanId,
    Guid CopyId,
    int DaysLate);

/// <summary>
/// The library stopped waiting for a copy, and somebody will be asked to replace it.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery.
/// </param>
/// <param name="BorrowerId">Who had it.</param>
/// <param name="LoanId">The loan written off.</param>
/// <param name="CopyId">The copy that never came back.</param>
/// <remarks>
/// The same domain event that tells Holdings a copy is unaccounted for tells this one that somebody
/// owes for it, and the two contracts carry different things: Holdings has no use for a borrower,
/// and this has no use for a status. One fact, two audiences, two flattenings — which is what keeps
/// either consumer from growing a use for what the other needed.
/// </remarks>
public sealed record LoanWrittenOff(
    Guid EventId,
    Guid BorrowerId,
    Guid LoanId,
    Guid CopyId);
