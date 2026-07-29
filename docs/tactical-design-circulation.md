# Tactical design — Circulation

The core context. Everything a library does with an item once it owns it: lending it, taking it
back, extending it, queuing people for it, and giving up on it.

Boundaries come from [strategic-design.md](strategic-design.md). This document decides aggregates,
invariants and the moments where they meet.

## 1. Policy

Every number the business can change lives in one place. None of them is a constant scattered through
an aggregate — a decision of the library must not be a deployment.

| Setting | Value | Note |
|---|---|---|
| `MaxConcurrentItems` | 5 | Loans **and** holds together, see §2 |
| `LoanDuration` | 21 days | |
| `MaxRenewals` | 2 | Refused outright if anyone is queuing, see §5 |
| `PickupPeriod` | 7 days | How long a trapped copy waits |
| `CourtesyReminder` | 3 days before due | |
| `OverdueReminders` | 1, 7, 14 days after due | |
| `DeclaredLostAfter` | 30 days after due | Ends the reminder chain |
| `BlockingDebt` | any amount owed | No threshold, by decision |

One flat set of values. There is no table per member category: a child and an adult may borrow the
same five items. The policy is still a value object rather than five constants, so indexing it by
category later is an addition rather than a rewrite.

## 2. The five-item cap

> `active loans` + `queued holds` + `holds awaiting pickup` ≤ 5

All three count. A trapped copy waiting on the hold shelf is immobilised for that borrower, so it
occupies a place exactly as a borrowed one does. The transition from hold to loan leaves the count
unchanged, so nothing has to be reconciled at pickup.

**The cap constrains the act, not the state.** It is checked inside `Checkout` and `PlaceHold`, and
nowhere else — never as an invariant of a borrower aggregate. If it were, lowering the cap from ten
to five would make every borrower holding eight items *invalid*, which is nonsense: they are in
perfect order, they borrowed under the previous rule. A cap is a rule about what may be added, not a
statement about what exists.

The count is therefore computed at the moment of the operation, not stored. That leaves a race — two
checkouts to the same borrower at the same instant could both pass the check and produce six items.
It is tolerated: the borrower is physically standing at one desk, and the consequence is a sixth
book. Should it ever matter, the fix is a small `BorrowerAccount` aggregate carrying the count, at
the price of a transaction spanning it and the loan.

Being at the cap does not prevent a **renewal**: nothing is added.

## 3. Standing

> A borrower who owes any amount may not **borrow**, **renew** or **place a hold**.
> A borrower may always **return**.

The last line is not a courtesy. A block that prevents returning creates the opposite incentive to
the one intended — the borrower keeps the item because there is nothing else to do with it — and it
is often the return itself that settles the debt.

The threshold lives here, in the circulation policy, not in Charges. Charges owns *what is owed*;
Circulation owns *what being owed forbids*. Charges exposes the balance; if it exposed `IsBlocked`,
the rule would have moved into the wrong context. The vocabulary keeps the two apart: `Balance` is
the Charges word for the amount, `Standing` is the Circulation word for the judgement, and neither
context ever utters the other's.

### A debt cancels existing holds

When a borrower incurs a debt, their queued holds are cancelled. Not suspended — removed. A borrower
who pays an hour later does not get their place back.

This produces an invariant stronger than the rule that creates it:

> **Nobody in a hold queue owes money.**

It is checkable at any instant, and it is what keeps a queue honest continuously rather than only at
the moment someone reaches the front.

The cancellation is driven by an event from Charges, not by a synchronous call. Apply the boundary
test: *can "this borrower owes money" and "their holds are gone" disagree for a few seconds without a
librarian noticing?* Yes — nobody is at the desk when a fine is assessed. Eventual consistency is
correct here.

The check at trapping time is kept anyway, as a net over the window between the debt being incurred
and Circulation reacting. It is one query, and it closes a gap that would otherwise put a copy on the
hold shelf for someone who cannot collect it.

**Consequence worth recording:** with no threshold and irreversible cancellation, twenty cents of
lateness costs a queue position waited for over months. That is the rule as decided. If staff are one
day seen waiving trivial fines to spare someone their place, this is the clause they are working
around.

## 4. Aggregates

### `Loan`

One copy, one borrower, one period. The root of everything that happens to an item while it is out.

```
Loan
  LoanId          identity
  CopyId          from Holdings — an identifier, never the copy itself
  BorrowerId      from Members  — an identifier, never the member
  CheckedOutOn
  DueDate
  RenewalCount
  ReturnedOn?
  Status          Active | Returned | Lost
  RemindersSent   which of the scheduled reminders have gone out
```

**Invariants.**

* `DueDate` is after `CheckedOutOn`.
* `RenewalCount` never exceeds `MaxRenewals`.
* A returned loan cannot be returned again, renewed, or declared lost.
* A loan declared lost is terminal.

`RemindersSent` is on the aggregate rather than in the notification layer for one reason: the nightly
scan must be able to run twice without sending anything twice, and only the loan knows what it has
already announced. It is a set, not a flag — adding a reminder to the schedule later must not be a
migration.

`Status` distinguishes `Returned` from `Lost` because loan statistics depend on it. A library's
budget is argued from its circulation figures, and an item that never came back is not a completed
loan.

### `HoldQueue`

One per **edition**, not per copy. A borrower waiting for *Le Petit Prince* wants the next copy, not
a particular volume — queuing on a copy would leave them waiting while an identical one goes back on
the shelf.

```
HoldQueue
  EditionId       identity — one queue per edition
  Holds           ordered, and only the live ones
    HoldId
    BorrowerId
    PlacedOn
    Status        Queued | AwaitingPickup
    TrappedCopyId?
    PickupDeadline?
```

**Invariants.**

* A borrower appears at most once in a queue.
* Queued holds are ordered by `PlacedOn`, and positions are contiguous.
* A trapped copy belongs to exactly one hold.
* Holds are promoted in placement order — the oldest queued hold is trapped first.

Note that several holds may be `AwaitingPickup` at once: three copies returning on the same morning
trap for the first three in the queue. What must never happen is the same copy being promised twice,
or the same borrower occupying two places.

**The queue is the aggregate, not the individual hold.** If each hold were its own root carrying a
position, "who is first" would be enforced by nothing: two returns on the same edition could promote
two people to the front, or trap two copies for the same one. Removing a hold from the middle — which
the debt rule does constantly — must close the gap behind it in the same breath, and only the queue
can do that.

The cost is one lock per edition. Two holds placed on the same popular title serialise. At library
scale that is invisible: holds arrive a few per minute, not a few per millisecond.

**A hold that ends leaves the aggregate.** Fulfilled, expired or cancelled, its outcome is published
as an event and kept by a history projection; the queue itself holds only what its invariants govern,
and every invariant above concerns live holds. This is not only purity: the queue is loaded on every
return of its edition — the most frequent operation of the day — and an aggregate that kept its own
past would grow without bound precisely on the hottest path. The history stays queryable where
history belongs, in a read model fed by the events.

**Refusals at placement.**

* Not on an edition the borrower already has on loan.
* Not twice in the same queue.
* Not while a copy is available on the shelf — that is a checkout, and allowing it would make the
  queue meaningless.
* Not while the borrower owes money.
* Not while at the five-item cap.

## 5. The moments

### Checkout

Preconditions, in order — cheapest and most likely to fail first:

1. The borrower is in good standing (their balance, queried from Charges, judged here).
2. The borrower is below the cap.
3. The copy exists and may be lent (query to Holdings: not reference-only, not in repair, not lost,
   not withdrawn).
4. The copy is not already on loan.
5. If the copy is trapped for a hold, it is trapped for *this* borrower.

Then: `Loan` is created, and if this checkout fulfils a hold, that hold is fulfilled and leaves the
queue — the outcome travels in the event, not in a status the queue keeps.

Step 5 is what stops a walk-in from being handed a copy someone is waiting for.

### Renewal

1. The loan is still active.
2. `RenewalCount` is below `MaxRenewals`.
3. The borrower is in good standing.
4. **The edition's hold queue is empty.**

The fourth is what makes a queue move. Without it a borrower renews indefinitely and the five people
behind them never get anything: the queue exists but does not turn, and a hold stops being a promise.

It is checked against the queue as it stands at that moment. A hold placed a minute after a renewal
does not undo it — the borrower acted in good faith on the state of the world, and revoking a granted
renewal is worse than making one person wait a period.

**The rule is blunt on purpose.** With four copies out and one hold, it denies four renewals to
satisfy one person. The refined version — refuse only as many renewals as there are holds to fill —
has to choose *which* borrowers are denied, and any answer to that is arbitrary and unexplainable at
the desk. Most library systems take the blunt rule for exactly this reason. It also bites rarely: a
public library holds one to three copies of most titles.

This rule and the refusal to place a hold while a copy is on the shelf protect each other. Without
the second, a single idle hold on a well-stocked edition would block every renewal of it.

### Return

The moment that justifies holds and loans living in one context. In a single transaction:

1. The `Loan` closes. Lateness is computed and recorded.
2. The edition's `HoldQueue` is asked whether the copy is wanted.
3. If it is, the oldest queued hold whose borrower is **in good standing** is trapped: the hold
   becomes `AwaitingPickup`, `TrappedCopyId` is set, `PickupDeadline` starts.
4. If it is not, the copy goes back to the shelf.

**Two aggregates change together, and that is deliberate.** The usual guidance — one aggregate per
transaction — is about consistency boundaries, and here the business genuinely requires both to agree
at every instant: a copy shelved that was promised, or a hold announced ready for a copy nobody set
aside, are both visible at the desk, to the member. Crossing two aggregates *inside one context* is a
considered choice. Crossing a context boundary in a transaction is not.

Both change in the command handler, on the aggregates, directly — never across an event handler. An
event is always handled later — the outbox stores it and a scheduler drains it — and an invariant
that waits is not an invariant. See [outbox.md](outbox.md).

Step 3 skips blocked borrowers rather than removing them, because at this point the debt event may
simply not have arrived yet. The removal is the debt handler's job.

### Cancelling a hold

A borrower changes their mind, at the desk. It is the ordinary exit from a queue, and it must exist:
a hold occupies one of the five places the cap counts, so a borrower who cannot free a place is
punished for having reserved at all.

1. A queued hold is removed, and the gap behind it closes — the same contiguity the debt rule
   exercises constantly.
2. A hold awaiting pickup is removed, and its trapped copy is released back to the queue, offered to
   the next borrower in good standing — exactly as an expiry releases it.
3. `HoldCancelled` is published. In the terms of §8 it is informational — the confirmation of an act
   the borrower chose — unlike `HoldsCancelledForDebt`, which announces a consequence they did not
   choose and always goes out.

No penalty attaches, for the same reasons §9 declines to punish the no-show.

### A debt is incurred

Circulation reacts to `MemberDebtIncurred` from Charges:

1. Every queued hold of that borrower is cancelled.
2. Every hold of theirs awaiting pickup is cancelled, and its trapped copy is released back to the
   queue — where it is offered to the next borrower in good standing.
3. `HoldsCancelledForDebt` is published, so the borrower learns why.

Step 2 matters: a trapped copy for a newly blocked borrower is precisely the waste the rule exists to
prevent, and leaving it on the shelf until its deadline would reintroduce it.

**Ordering.** A late return creates a debt *and* may trap a copy for the same borrower in another
queue. The debt must be settled first, or the system traps a copy and immediately releases it. In
practice this falls out of the event order — the return closes, the fine is assessed, the debt event
arrives — but it is worth stating, because reversing it produces a defect that looks random.

## 6. The scheduled process

Nothing above triggers an overdue.

> **The passage of time is not an event.** Something has to ask the question.

A daily run, owned by Circulation because deciding a loan is late is a circulation fact. It is not one
job but five queries, each idempotent — running it twice must change nothing and notify nobody twice.

| Query | Outcome |
|---|---|
| Loans due in 3 days, courtesy reminder not sent | `LoanDueSoon` |
| Loans overdue by 1, 7 or 14 days, that reminder not sent | `LoanBecameOverdue` |
| Loans overdue by 30 days | Declare lost: loan terminal, copy `Lost` in Holdings, `ReplacementCharge` in Charges |
| Trapped holds expiring tomorrow | `HoldExpiringSoon` |
| Trapped holds past their deadline | Expire, release the copy, promote the next in queue |

Idempotence rests entirely on `RemindersSent`. Without it the run either notifies daily — which
teaches borrowers to filter every message from the library, destroying the value of all of them — or
requires the notification layer to remember, which puts a circulation fact in a generic context.

This makes a controllable clock non-negotiable: none of these five queries is testable against the
system clock. `TimeProvider` is injected, never `DateTimeOffset.UtcNow`.

## 7. Events

Circulation publishes facts, never instructions. It does not know that anything sends email — the
day a channel changes, or a postal letter is added for borrowers without an address, nothing here
moves.

**Published.**

| Event | Consumed by |
|---|---|
| `LoanCheckedOut` | read model |
| `LoanRenewed` | read model, Notifications |
| `LoanReturned(…, daysLate)` | Charges, read model |
| `LoanDueSoon` | Notifications |
| `LoanBecameOverdue` | Notifications |
| `LoanDeclaredLost` | Holdings, Charges, Notifications |
| `RenewalRefused(…, reason)` | Notifications |
| `RenewalGranted(…, newDueDate)` | Notifications, read model |
| `HoldPlaced` | read model |
| `HoldReadyForPickup(…, pickupDeadline)` | Notifications |
| `HoldExpiringSoon` | Notifications |
| `HoldExpired` | Notifications |
| `HoldCancelled` | read model, Notifications |
| `HoldsCancelledForDebt` | Notifications |

`LoanReturned` carries `daysLate` and not a price. Whether a return was late is a circulation fact;
what lateness costs is a money question, and Charges answers it. A `daysLate` of zero is published
all the same — Charges decides there is nothing to charge.

`RenewalRefused` carries the reason, and it is not optional. There are three — the limit is reached,
the borrower owes money, someone is waiting — and they call for three different things from the
borrower. "We could not renew your loan" without saying which is a message that wastes a trip to the
library.

For the same reason the courtesy reminder should say whether the item can be renewed at all. Three
days before a due date on an edition with a queue, the useful message is not *"renew it"* but
*"someone is waiting, please bring it back"*.

**Consumed.**

| Event | From | Effect |
|---|---|---|
| `MemberDebtIncurred` | Charges | Cancel the borrower's holds |
| `MemberDebtCleared` | Charges | Nothing in the model — the borrower is simply able to act again |

## 8. Notifications

Circulation's events are facts; Notifications turns some of them into messages. Two things belong
here because they shape what Circulation must publish.

**Transactional or informational.** A message announcing a consequence the borrower did not choose —
a fine, a block, a cancelled hold, a copy ready — always goes out. A confirmation of a checkout or a
return is informational and may be declined. The distinction is made now because retrofitting it
means asking every member for a preference they were never offered.

**Three outcomes, not two.** Sent, failed, and *no channel available*. The third is not an error: a
member may legitimately have no email, and the system must surface a work list for staff to phone or
write rather than silently dropping them. For minors the contact is usually the guardian, which is a
`Members` concern — a member has an identity, and separately a way of being reached that may belong
to somebody else.

## 9. Deliberately left out

**A borrower who never collects is not penalised.** Each uncollected hold immobilises a copy for the
whole pickup period, and a borrower can repeat it at no cost — they owe nothing, so nothing blocks
them. Some libraries count no-shows and suspend the right to place holds after three or four.

Nothing is done about it, for three reasons. It is almost always forgetfulness rather than abuse, and
the *expiring tomorrow* reminder already addresses that. Charging for it would be wildly out of
proportion here: an uncollected hold would create a debt, which blocks the borrower, which cancels
every other hold they have — a forgotten errand costing them everything. And deferring is free: every
expiry is published and the history projection keeps it, so the day a librarian reports the problem
the count is already there, measured rather than guessed.

This is a rule to write when someone asks for it.

## 10. Open questions

* Does loan history stay in `Loan` forever, or is it archived? It is the only thing in the system
  that grows without bound.
