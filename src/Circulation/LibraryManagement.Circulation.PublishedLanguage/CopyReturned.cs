namespace LibraryManagement.Circulation.PublishedLanguage;

/// <summary>
/// A copy physically came back over the desk.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery.
/// </param>
/// <param name="CopyId">The copy in hand.</param>
/// <remarks>
/// <para>
/// The third flattening of one domain event: the return already leaves this context with the
/// lateness for whoever prices it, and this one carries the copy and nothing else, for whoever
/// keeps the stock. It exists because an object in hand cannot be unaccounted for, and the module
/// that records unaccounted-for copies must be able to agree with the shelf without a human
/// remembering to tell it.
/// </para>
/// <para>
/// Announced for every return, though almost every one changes nothing anywhere: the reader
/// whose record already matches the shelf has nothing to do, and deciding here which returns
/// matter would mean knowing another module's statuses. The same price
/// <c>MemberBalanceChanged</c> already pays in the other cycle, for the same reason.
/// </para>
/// </remarks>
public sealed record CopyReturned(Guid EventId, Guid CopyId);
