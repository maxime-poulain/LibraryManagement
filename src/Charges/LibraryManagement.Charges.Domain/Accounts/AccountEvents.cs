using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Charges.Domain.Accounts;

/// <summary>
/// A copy came back late, and the delay has been priced.
/// </summary>
/// <param name="MemberId">Who owes it.</param>
/// <param name="ChargeId">The charge.</param>
/// <param name="LoanId">The loan it prices.</param>
/// <param name="Amount">What the tariff made of the delay.</param>
/// <param name="DaysLate">
/// How late, carried beside the amount rather than instead of it. The amount is what is owed; the
/// days are why, and a member disputing a fine at the desk asks the second question — a message
/// that can only say <em>you owe two euros</em> sends the librarian to another screen.
/// </param>
public sealed record OverdueFineAssessed(
    MemberId MemberId,
    ChargeId ChargeId,
    LoanId LoanId,
    Money Amount,
    int DaysLate) : DomainEvent;

/// <summary>
/// The library gave up on a copy, and the replacement has been priced.
/// </summary>
/// <param name="MemberId">Who owes it.</param>
/// <param name="ChargeId">The charge.</param>
/// <param name="LoanId">The loan given up on.</param>
/// <param name="Amount">The replacement cost.</param>
public sealed record ReplacementChargeRaised(
    MemberId MemberId,
    ChargeId ChargeId,
    LoanId LoanId,
    Money Amount) : DomainEvent;

/// <summary>
/// Money was received against what a member owes.
/// </summary>
/// <param name="MemberId">Who paid.</param>
/// <param name="Amount">How much.</param>
/// <remarks>
/// Distinct from <see cref="ChargeWaived"/> and deliberately so: one is money received and the other
/// money forgone, and a library counts the two apart. Merging them into a single reduction with a
/// reason code would make <em>how much did we take in this month</em> a question about a flag,
/// which is the first question a treasurer asks.
/// </remarks>
public sealed record PaymentTaken(MemberId MemberId, Money Amount) : DomainEvent;

/// <summary>
/// A charge was cancelled by a decision rather than by payment.
/// </summary>
/// <param name="MemberId">Whose charge.</param>
/// <param name="ChargeId">The charge.</param>
/// <param name="Amount">What was forgone — what remained outstanding when it was cancelled.</param>
public sealed record ChargeWaived(
    MemberId MemberId,
    ChargeId ChargeId,
    Money Amount) : DomainEvent;

/// <summary>
/// A member's balance moved.
/// </summary>
/// <param name="MemberId">Whose balance.</param>
/// <param name="PreviousBalance">What it was.</param>
/// <param name="CurrentBalance">What it is now.</param>
/// <remarks>
/// <para>
/// One event carrying both amounts, and no word about owing. This context states what its own
/// arithmetic knows; whether anything was crossed is read on arrival, by the context that owns the
/// threshold.
/// </para>
/// <para>
/// The obvious shape was a pair — <em>became owing</em>, <em>settled</em> — and it was lacunary. Two
/// crossings, both of zero, is complete for a threshold of zero and for no other value: a balance
/// moving from twenty cents to twelve euros crosses a ten-euro line and would have announced
/// nothing, so the rule would stop firing with no error and no failing test. Carrying the previous
/// amount is also what spares the reader from remembering it — Circulation stores no standing and
/// compares no history.
/// </para>
/// </remarks>
public sealed record MemberBalanceChanged(
    MemberId MemberId,
    Money PreviousBalance,
    Money CurrentBalance) : DomainEvent;
