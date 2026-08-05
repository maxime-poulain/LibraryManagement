# Tactical design — Charges

Money owed to the library, and the decisions that create, cancel or settle it. The context prices
facts it is told about and never establishes them: whether a return was late is Circulation's
observation, and what an amount forbids is Circulation's judgement. Charges knows a `Balance` and
says neither `Debt` nor `Standing`.

Boundaries come from [strategic-design.md](strategic-design.md). This document decides aggregates,
invariants and the moments where they meet.

## 1. The tariff

| Setting | Value | Note |
|---|---|---|
| `FinePerDayOverdue` | €0.20 | |
| `GracePeriod` | 0 days | A lever set to nothing, see below |
| `MaxFinePerLoan` | €10.00 | A cap, see §3 |
| `ReplacementCharge` | €25.00 | Flat, until a copy carries a value — §10 |

Every number the business can change lives in one place, for Circulation's reason: a decision of the
library must not be a deployment. And this context's numbers change more often than any other's —
an amnesty, an exemption, a municipal decision on the tariff — which is what makes the policy object
worth more here than the one lever Members has.

**The grace period is a lever set to zero, not an absent concept.** Naming it now means introducing
one is a change of data; leaving it out would make it a change of model. The library's chosen
kindness today is the courtesy reminder three days before the due date — Circulation's, and enough.

**No table per member category.** A child and an adult are fined the same, exactly as they may borrow
the same five copies. The policy is a value object all the same, so indexing it later is an addition
rather than a rewrite — the reserve Circulation's flat policy already records.

## 2. The aggregate

One aggregate, `MemberAccount`, keyed by the member. The consistency boundary is a person's money,
because that is what every rule here reads: a payment allocated across several charges, and the
balance that decides whether the account crossed into owing, are one decision that has to be made in
one breath. Charges as independent aggregates would put that decision between two transactions.

```
MemberAccount
  MemberId          identity — the same Guid Members issued, redeclared locally
  Charges           the outstanding ones, and only those, see below
  Balance           the sum of what is outstanding, computed, see below
```

```
Charge                        an entity within the account
  ChargeId
  Amount                      what was charged, see §4
  Paid                        how much has been settled against it
  IncurredOn
  LoanId                      the circulation fact it prices
```

**Invariants.**

* A charge's `Amount` is positive. Zero is not a charge, it is the absence of one.
* `Paid` never exceeds `Amount`.
* `Balance` is never negative — an overpayment is refused rather than turned into credit (§9).
* A charge belongs to exactly one account, and the account is the member's.

**The account holds what is outstanding, not a ledger.** A charge fully paid or waived leaves the
aggregate; the event that ended it is published, and the read model staff consult keeps the trail.
This is `HoldQueue`'s rule on a colder path — an aggregate holds only what its invariants govern —
and it is what stops a member's account from becoming the second thing in the system that grows
without bound. The alternative, keeping every settled charge forever, would load a decade of
€0.40 fines to answer *may this person borrow*.

**`Balance` is computed, never stored.** It is the sum of `Amount − Paid` over what the account
holds, and there is no field to fall out of step with the charges beneath it. The same omission as
Members' entitlement, for the same reason: a stored total is a second source of truth for something
already derivable, and the day it disagrees, nothing says which is right.

**The desk reads it without loading the account.** Circulation asks *how much does this member owe*
on every checkout and every hold, which is the most frequent write path of the system (§6). That
question is answered by a query summing the outstanding charges — the read side, materializing no
aggregate — exactly as Catalog answers a search without loading a `Work`.

## 3. A replacement charge is not a fine

The strategic design refuses to merge them, and the tactical consequence is two types rather than
one entity with a kind:

```
OverdueFine          days late × the rate, capped
ReplacementCharge    a flat figure, the object itself
```

They differ in every direction that matters. The **amount** is a rate applied to a duration in one
case and a figure in the other. The **occasion** is a return in one case and the library giving up
in the other — and those are two different events from Circulation. The **magnitude** differs by two
orders, which is why one is waived at the desk as a routine courtesy and the other is a decision
somebody signs for. And the **ending** differs: a fine ends when it is paid, while a replacement
charge has a second ending nobody has modeled yet, the day the book turns up (§10).

One type with a `Kind` enum would put both tariffs in one method and both waiver policies in one
rule, and the day the library exempts fines for a month it would have to say *which* kind it meant.

Stored table-per-hierarchy with the kind as its name rather than a number, the rule that a status a
human reads in a table is stored as a string.

**The cap is what a fine is worth, not what the copy is.** `MaxFinePerLoan` exists because a fine is
charged for time and time is unbounded: without it, a copy returned two years late would owe more
than replacing it, which no library charges and no member would pay. It is a ceiling on the fine
alone and has nothing to say about the replacement charge.

**A fine is assessed on return, never accrued daily.** There is no job here counting money upward
while a copy is out. Circulation publishes the return with `daysLate`, and that is the only moment
an overdue fine exists — so a copy still out owes nothing yet, and a copy never returned owes no
fine at all, because it becomes a replacement charge at thirty days instead. The two occasions are
disjoint by construction, and no rule is needed to keep them so.

## 4. Money

A `Money` value object carrying an amount, and no currency.

**One library, one currency.** A currency field that always says EUR is a field nobody validates and
everybody trusts, and the first multi-currency requirement would rewrite the arithmetic anyway rather
than fill the field in. §10 keeps it as a reservation instead of a pretence.

`decimal` and never `double`: money is counted in hundredths, and binary floating point cannot
represent a fifth of a euro. Two decimal places, rounded half away from zero — the rule a till uses.

**`Money` never crosses a boundary.** The port Circulation declared answers with a `decimal`,
because a published language traffics in primitives; the value object stays inside the context, where
its arithmetic is worth having.

## 5. Payment and waiver are two acts, not one

Both reduce what is owed and the library counts them apart: one is money received, the other is
money forgone. Merging them into a single reduction with a reason code would make *how much did we
take in this month* a question about a flag, which is exactly the question a treasurer asks first.

So: two operations, two events, and a read model that can tell them apart without parsing anything.

**A payment settles oldest first.** It is the convention, and it is the one that keeps an ancient
forty-cent fine from following a member for years while newer ones are cleared. A payment naming a
particular charge is an addition the day the desk asks for it, not a second concept.

**A waiver names one charge.** Cancelling a whole balance in one act is not a waiver but an amnesty,
which is a different decision at a different level and is left out deliberately (§9).

## 6. The two ways Circulation and Charges speak

The context map draws this pair as the one cycle needing an inversion, and the boundary test is what
separates the two directions.

**Synchronous, at the desk: how much does this member owe?** Circulation declares the port in its own
published language and Charges implements it — the anticorruption layer belonging to the downstream,
which for this one question is Circulation. It answers an amount and never a verdict.

```
IMemberBalance.OwedByAsync(memberId) → decimal
```

**Asynchronous, afterwards: the facts.** Circulation announces what happened; Charges prices it.
Nobody is at the desk when a fine is assessed, and a fine that failed to be created is reconciled
rather than noticed.

```
LoanReturned(…, daysLate)  →  an overdue fine, when daysLate is positive
LoanDeclaredLost(…)        →  a replacement charge
```

And back the other way, when the account crosses a line:

```
zero → owing    MemberBalanceBecameOwing
owing → zero    MemberBalanceSettled
```

**These two events are the one place this context comes close to judging.** Circulation cancels a
borrower's holds on the first and lets them act again on the second, and the crossing point is zero
— which is right *only* because Circulation's own `BlockingDebt` is "any amount owed", with no
threshold. The two agree today by decision rather than by accident. If Circulation ever adopted a
threshold, these events would become Charges' opinion of a Circulation rule, and the honest shape
would then be a single `MemberBalanceChanged` carrying the amount, leaving the crossing to the
context that owns the rule. Recorded here so that change is a decision rather than a discovery.

## 7. The moments

### Assess an overdue fine

Driven by `LoanReturned`, and only when `daysLate` is positive — a return on time is published all
the same and priced at nothing, because Charges decides there is nothing to charge.

The amount is `(daysLate − GracePeriod) × FinePerDayOverdue`, floored at zero and capped at
`MaxFinePerLoan`. A fine computed to zero raises no charge at all: zero is not a charge (§2), and an
account holding one would report a member as owing while the balance said nothing.

### Raise a replacement charge

Driven by `LoanDeclaredLost`. A flat figure, and the loan is named so the read model can say what it
was for.

Both of these arrive through the passage [outbox.md](outbox.md) §9 describes: a flat contract from
Circulation's published language, turned into a command of this module, saved in this module's own
transaction. Both are therefore **idempotent by the loan they price** — a redelivery must not charge
a member twice, and the account refusing a second charge for a loan it already priced is what makes
that true without a deduplication table.

### Take a payment

Money received, allocated oldest first (§5). Preconditions, cheapest first:

1. The amount is positive.
2. The amount does not exceed the balance (§2 — no credit).

Charges settled to nothing leave the account, each announcing its own ending, and the account
announces the balance reaching zero when it does.

### Waive a charge

A charge cancelled by a decision. It names one charge, and the charge leaves the account exactly as
a paid one does — the difference is which event is published, and that difference is the point (§5).

### Open an account

Not a moment anyone performs. An account exists because a member has been charged, and it is created
by the first charge rather than by an enrollment: an account for every member who never owed anything
is a row per member forever, answering a question nobody asks. `NoSuchAccount` and a balance of zero
are the same answer to the desk, and the port returns zero for both.

## 8. Events

**Published.**

| Event | Consumed by |
|---|---|
| `OverdueFineAssessed(memberId, chargeId, loanId, amount, daysLate)` | read model, Notifications |
| `ReplacementChargeRaised(memberId, chargeId, loanId, amount)` | read model, Notifications |
| `PaymentTaken(memberId, amount, settled)` | read model |
| `ChargeWaived(memberId, chargeId, amount)` | read model |
| `MemberBalanceBecameOwing(memberId, balance)` | Circulation, Notifications |
| `MemberBalanceSettled(memberId)` | Circulation |

`OverdueFineAssessed` carries `daysLate` as well as the amount. The amount is what is owed; the days
are why, and a member disputing a fine at the desk asks the second question. A message that can only
say *you owe two euros* sends the librarian to another screen.

**Consumed.**

| Event | From | Effect |
|---|---|---|
| `LoanReturned(…, daysLate)` | Circulation | Assess an overdue fine, when there is one to assess |
| `LoanDeclaredLost` | Circulation | Raise a replacement charge |

## 9. Deliberately left out

**Credit balances and refunds.** An overpayment is refused rather than held, and no operation gives
money back. Both are real in a library that takes cash, and both bring a second sign into every
arithmetic in this document — a balance that may be negative, an allocation that may run backwards,
a `Standing` rule that has to say what a credit means. Refusing the overpayment is one line and
costs a librarian one correction; supporting it costs the model its floor. To revisit when the desk
reports it happening.

**The till.** Cash drawers, reconciliation at close of day, who was on the desk, what was taken in
by which method. All real, all a point-of-sale concern rather than a domain one, and the moment
`PaymentTaken` records is where the two would meet. Named so that a till one day integrates rather
than expands this context.

**Amnesty.** Cancelling every fine in the library — after a flood, at a change of policy, for a
week in the autumn — is a decision at a level this context has no aggregate for: it spans every
account. It is a batch of waivers when it arrives, and the waiver is already here to carry it.

**Membership fees.** Members §9 already places them: the fee would be a charge created when Members
announces an enrollment or a renewal, the `LoanReturned` shape exactly. Nothing here forbids it, and
nothing here anticipates it either.

**Interest, escalation, collections.** A debt does not grow while it is unpaid, and nobody is
referred anywhere. `MaxFinePerLoan` is the whole of this context's opinion on how large a small debt
may become.

## 10. Consequences and open questions

Nothing is built yet, so this section carries no *what building it taught* — the other tactical
designs earned theirs by being implemented, and this one will.

Open, each deferred for a stated reason rather than forgotten:

* **A copy found after it was paid for.** Holdings can undo `Lost`, and a member who paid €25 for a
  book that turns up has a claim nobody has modeled. It is the second ending §3 mentions, and it
  needs an edge the context map does not carry — Holdings announcing a copy found, to a context that
  today only hears from Circulation. Whether the answer is a refund, a credit, or a waiver of what is
  still outstanding depends on the refund question above, which is why the two are deferred together.
* **What a copy is worth.** `ReplacementCharge` is one flat figure for a paperback and for a folio,
  because no context records what a copy cost. An acquisition price belongs in Holdings — it is a
  fact about this library's object, not about the edition — and the day it exists this setting
  becomes a default rather than the answer.
* **Erasure, made worse by money.** Members §10 already asks what happens to an erased member who
  still owes; this context is the other half of that question. A balance is a claim, and a claim that
  must be kept for accounting is a reason not to erase — which is a legal answer and not a modeling
  one, unanswered until someone who knows the law is in the room.
* **Currency**, if this library ever holds one that is not the euro (§4).
* **Whether a waiver needs a reason.** Today it is a decision recorded without a justification, on
  the argument that a librarian waiving forty cents should not have to write an essay. A library
  auditing its waivers would want one, and the field is an addition — but the day it becomes
  mandatory it changes what the operation *is*, from a courtesy to a filing.
