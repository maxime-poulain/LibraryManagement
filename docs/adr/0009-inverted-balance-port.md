# ADR-0009 — Circulation declares the balance port; Charges implements it

- **Status**: Accepted
- **Date**: 2026-08-06
- **Authority**: [`strategic-design.md`](../strategic-design.md) §8,
  [`tactical-design-charges.md`](../tactical-design-charges.md) §6

## Context

Circulation and Charges form a cycle in the context map, and it is deliberate. Circulation is
upstream for the facts it observes — a return, a loss — and downstream for what those facts cost.
Two flows run the other way, and the boundary test separates them:

- **A new debt cancels the borrower's holds.** Can "this member owes money" and "their holds are
  gone" disagree for a few seconds unnoticed? Yes — nobody is at the desk when a fine is assessed.
  An **event**, asynchronous.
- **How much does this member owe?** Asked at the counter, with the member standing there having
  possibly just paid. A projection lag would be visible. A **query**, synchronous.

The synchronous half is the problem: a direct reference would put the cycle into the assembly graph.

## Decision

**Circulation declares the port; Charges implements it.**
`LibraryManagement.Circulation.PublishedLanguage.IMemberBalance` is declared by the *consumer*, and
`Charges.Infrastructure` provides the implementation against its own store.

```csharp
ValueTask<decimal> OwedByAsync(Guid memberId, CancellationToken cancellationToken = default);
```

**It answers with an amount and never with a verdict.** What being owed forbids — the borrower's
`Standing` — is judged on the asking side, by the circulation policy. If the port exposed
`IsBlocked`, the rule would have moved into the wrong context.

The other direction stays an event: Charges publishes `MemberBalanceChanged(memberId,
previousBalance, currentBalance)` and Circulation decides what was crossed.

## Consequences

The assembly graph stays acyclic. Charges references Circulation's published language; Circulation
references nothing of Charges. The anticorruption layer belongs to the downstream context, which for
this one question is Circulation.

**This is not an Open Host Service**, though it looks like one. An OHS is a protocol published *by the
upstream* for an open set of consumers — Holdings' lendability question is one. Here the downstream
wrote the contract for its own single use, and the distinction is worth keeping because it predicts
who may change it.

The vocabulary splits cleanly and stays split: `Balance` is the Charges word, `Debt` and `Standing`
are Circulation's, and the port speaks neither — it hands over a number.

**The threshold is named on Circulation's side only, which is what makes it movable.** An event
announcing *became owing* would carry Charges' assumption that the line is zero, and moving the line
would mean changing what the other context publishes. This is not hypothetical: the design *did*
first specify `MemberBalanceBecameOwing` / `MemberBalanceSettled`, and writing the other side showed
the pair reports two crossings, both of zero — complete for `BlockingDebt` as it stands and for no
other value. A balance moving from twenty cents to twelve euros crosses a ten-euro threshold and
would have announced nothing: the rule would stop firing with no error, no failing test and no line
in a log.

The cost is that Circulation is woken for movements it will ignore — a few dozen a day in a municipal
library, and the price of never having to ask Charges what a threshold is.

This synchronous edge sits on the most frequent write path in the system, which is one of the two
reasons [ADR-0001](0001-modular-monolith-over-services.md) records for the deployment staying
monolithic.

## Alternatives rejected

**Charges exposes `IsBlocked`.** One method instead of a judgement, and the circulation policy moves
into the money context.

**Circulation subscribes and caches the balance.** It removes the synchronous edge and reintroduces a
projection lag at the desk, with a member who has just paid still refused.

**A pair of transition events** instead of the amounts. Rejected above, and recorded at length in
[`tactical-design-charges.md`](../tactical-design-charges.md) §6 because the failure mode is silence.
