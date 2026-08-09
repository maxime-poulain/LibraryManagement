namespace LibraryManagement.Circulation.PublishedLanguage;

/// <summary>
/// A copy came back spoiled, and the desk said so while closing the loan.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery.
/// </param>
/// <param name="BorrowerId">Who brought it back. The same value Members issued for the person.</param>
/// <param name="LoanId">The loan whose return carried the observation.</param>
/// <param name="CopyId">The copy that came back worse than it went out.</param>
/// <remarks>
/// The observation and never a price: what a spoiled copy costs is the reader's tariff, exactly
/// as lateness is. Published only when the desk observed damage — unlike the return itself, which
/// is announced punctual and late alike, this fact simply does not occur for the ordinary
/// return, so there is nothing to filter and no judgement smuggled into the filtering.
/// </remarks>
public sealed record CopyReturnedDamaged(
    Guid EventId,
    Guid BorrowerId,
    Guid LoanId,
    Guid CopyId);
