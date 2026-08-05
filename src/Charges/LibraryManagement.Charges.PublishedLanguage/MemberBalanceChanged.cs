namespace LibraryManagement.Charges.PublishedLanguage;

/// <summary>
/// A member's balance moved.
/// </summary>
/// <param name="EventId">
/// The identity of the occurrence, so a subscriber can recognise a redelivery. Delivery beyond a
/// module is at-least-once and this is what deduplicating is keyed on.
/// </param>
/// <param name="MemberId">Whose balance. The same value Circulation holds as its borrower.</param>
/// <param name="PreviousBalance">What was owed before.</param>
/// <param name="CurrentBalance">What is owed now.</param>
/// <remarks>
/// <para>
/// <strong>Both amounts, and no word about owing.</strong> Charges states what its own arithmetic
/// knows; whether anything was crossed is decided on arrival, by the context that owns the
/// threshold. Carrying the previous amount is what spares the reader from remembering it — the
/// subscriber stores no standing and compares no history.
/// </para>
/// <para>
/// The obvious shape was a pair of transitions, <em>became owing</em> and <em>settled</em>, and it
/// was lacunary: two crossings, both of zero, is complete for a threshold of zero and for no other
/// value. A balance moving from twenty cents to twelve euros crosses a ten-euro line and would have
/// announced nothing, so the rule would have stopped firing with no error and no failing test.
/// </para>
/// <para>
/// Amounts travel as <see cref="decimal"/>: this project references nothing, and the money value
/// object stays where its arithmetic is worth having.
/// </para>
/// </remarks>
public sealed record MemberBalanceChanged(
    Guid EventId,
    Guid MemberId,
    decimal PreviousBalance,
    decimal CurrentBalance);
