# Tactical design — Circulation

The core context. Everything a library does with a copy once it owns it: lending it, taking it
back, extending it, queuing people for it, and giving up on it.

Boundaries come from [strategic-design.md](strategic-design.md). This document decides aggregates,
invariants and the moments where they meet.

## 1. Policy

Every number the business can change lives in one place. None of them is a constant scattered through
an aggregate — a decision of the library must not be a deployment.

| Setting | Value | Note |
|---|---|---|
| `MaxLoansAndHolds` | 5 | Loans **and** holds together, see §2 |
| `LoanDuration` | 21 days | |
| `MaxRenewals` | 2 | Refused outright if anyone is queuing, see §5 |
| `PickupPeriod` | 7 days | How long a trapped copy waits |
| `CourtesyReminder` | 3 days before due | |
| `OverdueReminders` | 1, 7, 14 days after due | |
| `DeclaredLostAfter` | 30 days after due | Ends the reminder chain |
| `BlockingDebt` | any amount owed | No threshold, by decision |

One flat set of values. There is no table per member category: a child and an adult may borrow the
same five copies. The policy is still a value object rather than five constants, so indexing it by
category later is an addition rather than a rewrite.

## 2. The cap of five

> `active loans` + `queued holds` + `holds awaiting pickup` ≤ 5

All three count. A trapped copy waiting on the hold shelf is immobilized for that borrower, so it
occupies a place exactly as a borrowed one does. The transition from hold to loan leaves the count
unchanged, so nothing has to be reconciled at pickup.

**The setting is named after what it counts.** It was `MaxConcurrentItems`, and none of the three
things it counts is an item: a hold is a claim on an *edition*, and `Item` is in any case the word
FRBR uses for what this model calls a `Copy`. `MaxLoansAndHolds` is blunt and cannot be misread,
which is the whole requirement of a policy setting.

**The cap constrains the act, not the state.** It is checked inside `Checkout` and `PlaceHold`, and
nowhere else — never as an invariant of a borrower aggregate. If it were, lowering the cap from ten
to five would make every borrower holding eight copies *invalid*, which is nonsense: they are in
perfect order, they borrowed under the previous rule. A cap is a rule about what may be added, not a
statement about what exists.

The count is therefore computed at the moment of the operation, not stored. That leaves a race — two
checkouts to the same borrower at the same instant could both pass the check and produce six copies.
It is tolerated: the borrower is physically standing at one desk, and the consequence is a sixth
book. Should it ever matter, the fix is a small `BorrowerAccount` aggregate carrying the count, at
the price of a transaction spanning it and the loan.

Being at the cap does not prevent a **renewal**: nothing is added.

## 3. Standing

> A borrower who owes any amount may not **borrow**, **renew** or **place a hold**.
> A borrower may always **return**.

The last line is not a courtesy. A block that prevents returning creates the opposite incentive to
the one intended — the borrower keeps the copy because there is nothing else to do with it — and it
is often the return itself that settles the debt.

The threshold lives here, in the circulation policy, not in Charges. Charges owns *what is owed*;
Circulation owns *what being owed forbids*. Charges exposes the balance; if it exposed `IsBlocked`,
the rule would have moved into the wrong context. The vocabulary keeps the two apart: `Balance` is
the Charges word for the amount, `Standing` is the Circulation word for the judgement, and neither
context ever utters the other's.

`Debt` is the third word, and it belongs **here**. It names the same figure as `Balance`, seen as
something that forbids rather than something that is owed — which is why it appears in
`BlockingDebt` and in `HoldsCancelledForDebt` and never in anything Charges publishes. Charges
announces `MemberBalanceBecameOwing`; Circulation reads it and forms its own `Debt` and its own
`Standing`. That translation is the anticorruption layer doing its job, and it is what keeps the
rule above from being a slogan.

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

One copy, one borrower, one period. The root of everything that happens to a copy while it is out.

```
Loan
  LoanId          identity
  CopyId          from Holdings — an identifier, never the copy itself
  EditionId       from Catalog  — the queue this copy answers to, recorded at checkout
  BorrowerId      from Members  — an identifier, never the member
  CheckedOutOn
  DueDate
  RenewalCount
  ReturnedOn?
  Status          Active | Returned | DeclaredLost
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

`Status` distinguishes `Returned` from `DeclaredLost` because loan statistics depend on it. A
library's budget is argued from its circulation figures, and a copy that never came back is not a
completed loan.

The participle carries the difference from Holdings' own `Lost`, and it is not a nicety. A copy is
`Lost` when nobody knows where it is — a state discovered. A loan is `DeclaredLost` when the library
decides, after thirty days, to stop waiting — a state chosen. Calling both `Lost` would put the same
word on an observation and on a decision.

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

The cost is one lock per edition. Two holds placed on the same popular title serialize. At library
scale that is invisible: holds arrive a few per minute, not a few per millisecond.

**A hold that ends leaves the aggregate.** Fulfilled, expired or cancelled, its outcome is published
as an event and kept by a history projection; the queue itself holds only what its invariants govern,
and every invariant above concerns live holds. This is not only purity: the queue is loaded on every
return of its edition — the most frequent operation of the day — and an aggregate that kept its own
past would grow without bound precisely on the hottest path. The history stays queryable where
history belongs, in a read model fed by the events.

**Refusals at placement.**

* Not by someone the registry does not know, or whose membership has lapsed — the same entitlement
  question the checkout opens with.
* Not on an edition the borrower already has on loan.
* Not twice in the same queue.
* Not while a copy is available on the shelf — that is a checkout, and allowing it would make the
  queue meaningless.
* Not while the borrower owes money.
* Not while at the cap of five.

## 5. The moments

### Checkout

Preconditions, in order — cheapest and most likely to fail first:

1. The borrower is entitled to borrow (query to Members: enrolled, and the membership current).
   The check the strategic design always implied and this list once omitted, added when the
   module was built: a person the registry does not know cannot be judged for debt, which is also
   why it runs first.
2. The borrower is in good standing (their balance, queried from Charges, judged here).
3. The copy exists and may be lent (query to Holdings: not reference-only, not in repair, not lost,
   not withdrawn).
4. The copy is not already on loan.
5. If the copy is trapped for a hold, it is trapped for *this* borrower.
6. The borrower is below the cap — last, not second as this list first had it, and skipped
   entirely when the checkout collects the borrower's own trapped hold: the transition from hold
   to loan leaves the count unchanged (§2), so a pickup is never refused for the cap.

Then: `Loan` is created, and if this checkout fulfills a hold, that hold is fulfilled and leaves the
queue — the outcome travels in the event, not in a status the queue keeps.

Step 5 is what stops a walk-in from being handed a copy someone is waiting for.

### Renewal

1. The loan is still active.
2. `RenewalCount` is below `MaxRenewals`.
3. The borrower is in good standing.
4. **Nobody is queued on the edition.**

The fourth is what makes a queue move. Without it a borrower renews indefinitely and the five people
behind them never get anything: the queue exists but does not turn, and a hold stops being a promise.
It reads *queued* deliberately, where this document first said *empty*: a claim already awaiting
pickup has its copy on the hold shelf, and refusing a renewal for its sake would serve nobody the
rule exists to serve. The renewal grants another loan period from the current due date — the
arithmetic the membership renewal decided, for the same reason: renewing early costs nothing.

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

Circulation reacts to `MemberBalanceBecameOwing` from Charges:

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
| Loans due within 3 days, courtesy reminder not sent | `LoanDueSoon` |
| Loans overdue by at least 1, 7 or 14 days, that reminder not sent | `LoanBecameOverdue` |
| Loans overdue by 30 days | Declare lost: loan terminal, copy `Lost` in Holdings, `ReplacementCharge` in Charges |
| Trapped holds expiring today or tomorrow, not yet warned | `HoldExpiringSoon` |
| Trapped holds past their deadline | Expire, release the copy, promote the next in queue |

**Every stage reads as *at least*, not exactly.** A run that did not happen for three days finds a
loan nine days late with neither the first nor the seventh stage announced. It marks both spent and
sends one message, naming the real nine days. On an exact reading the missed stages would be owed to
the borrower forever and never sent; announcing each of them in turn would deliver three messages in
one morning, which is the noise the schedule exists to avoid. The same reading gives the courtesy
reminder its window rather than its day, and the imminent-expiry warning the two days above:
a deadline is a date the run must not step over, and a run only ever fires once a day.

Idempotence rests entirely on what the aggregates remember. `RemindersSent` carries it for loans;
a hold keeps its own `ExpiryWarningSent`, because a hold's schedule is not the loan's and a shared
memory would tie two unrelated cadences together. Without it the run either notifies daily — which
teaches borrowers to filter every message from the library, destroying the value of all of them — or
requires the notification layer to remember, which puts a circulation fact in a generic context.

Renewing clears the memory. A renewal moves the due date, so every appointment recorded against the
old one is about a date that no longer exists; kept, they would silence the courtesy reminder for the
whole of the new period.

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
| `LoanReturned(…, daysLate)` | Charges, read model |
| `LoanDueSoon(…, anyoneIsWaiting)` | Notifications |
| `LoanBecameOverdue(…, daysOverdue)` | Notifications |
| `LoanDeclaredLost` | Holdings, Charges, Notifications |
| `RenewalRefused(…, reason)` | Notifications |
| `RenewalGranted(…, newDueDate)` | Notifications, read model |
| `HoldPlaced` | read model |
| `HoldFulfilled` | read model |
| `HoldReadyForPickup(…, pickupDeadline)` | Notifications |
| `HoldExpiringSoon` | Notifications |
| `HoldExpired` | Notifications |
| `HoldCancelled` | read model, Notifications |
| `HoldsCancelledForDebt` | Notifications |

This table once listed `LoanRenewed` beside `RenewalGranted`. They were one fact under two names —
a renewal succeeded and the due date moved — and a reader had no way to tell which to subscribe to.
`RenewalGranted` is the one, because it names the outcome and carries the new date; nothing was ever
published under the other name.

`LoanBecameOverdue` carries the days actually elapsed and not the stage that fired it. A borrower
told they are nine days late can act; one told they have reached "stage two" cannot, and after a run
that missed a day or two the stage is no longer even true.

`LoanReturned` carries `daysLate` and not a price. Whether a return was late is a circulation fact;
what lateness costs is a money question, and Charges answers it. A `daysLate` of zero is published
all the same — Charges decides there is nothing to charge.

`RenewalRefused` carries the reason, and it is not optional. There are three — the limit is reached,
the borrower owes money, someone is waiting — and they call for three different things from the
borrower. "We could not renew your loan" without saying which is a message that wastes a trip to the
library.

For the same reason the courtesy reminder should say whether the loan can be renewed at all. Three
days before a due date on an edition with a queue, the useful message is not *"renew it"* but
*"someone is waiting, please bring it back"*.

**Consumed.**

| Event | From | Effect |
|---|---|---|
| `MemberBalanceBecameOwing` | Charges | Cancel the borrower's holds |
| `MemberBalanceSettled` | Charges | Nothing in the model — the borrower is simply able to act again |

## 8. Notifications

Circulation's events are facts; Notifications turns some of them into messages. Two things belong
here because they shape what Circulation must publish.

**Consequential or courtesy.** A message announcing a consequence the borrower did not choose —
a fine, a block, a cancelled hold, a copy ready — always goes out. A confirmation of a checkout or a
return is a courtesy and may be declined. The distinction is made now because retrofitting it
means asking every member for a preference they were never offered.

Not *transactional*, the word the messaging industry uses for this split: it means a database
transaction everywhere else in this repository — the whole of [outbox.md](outbox.md) rests on the
other sense — and `CourtesyReminder` in §1 already gives the pair its second half.

**Three outcomes, not two.** Sent, failed, and *no channel available*. The third is not an error: a
member may legitimately have no email, and the system must surface a work list for staff to phone or
write rather than silently dropping them. For minors the contact is usually the guardian, which is a
`Members` concern — a member has an identity, and separately a way of being reached that may belong
to somebody else.

## 9. Deliberately left out

**A borrower who never collects is not penalised.** Each uncollected hold immobilizes a copy for the
whole pickup period, and a borrower can repeat it at no cost — they owe nothing, so nothing blocks
them. Some libraries count no-shows and suspend the right to place holds after three or four.

Nothing is done about it, for three reasons. It is almost always forgetfulness rather than abuse, and
the *expiring tomorrow* reminder already addresses that. Charging for it would be wildly out of
proportion here: an uncollected hold would create a debt, which blocks the borrower, which cancels
every other hold they have — a forgotten errand costing them everything. And deferring is free: every
expiry is published and the history projection keeps it, so the day a librarian reports the problem
the count is already there, measured rather than guessed.

This is a rule to write when someone asks for it.

## 10. Consequences and open questions

**What building the desk moments added.** The five synchronous moments — checkout, return,
renewal, placing and cancelling a hold — are implemented, and so is §6's scheduled process and the
first fact this context announces beyond itself. Only the reactions to Charges' events remain, and
they wait on Charges rather than on any mechanism. Building the desk taught four things this document
now carries in place: the entitlement precondition the checkout list omitted, the cap moving
after the trap resolution, the renewal rule reading *queued* rather than *empty*, and
`HoldFulfilled` joining the events table — §4 promised every outcome an event, and the table had
skipped the happiest one.

Two things the design did not anticipate, settled by the code:

* **The loan records its `EditionId`.** The return and every renewal must name the queue the copy
  answers to, and the loan is the only thing at hand that can remember it. Holdings' lendability
  answer now carries the edition — the Customer/Supplier contract change the context map priced
  in advance — and the loan keeps it from checkout on, as the queue the copy played in whatever
  Catalog later does to the edition.
* **`RenewalRefused` cannot be published as designed.** The pipeline writes the outbox in the
  same save as the change, and saves only when the command succeeds — a refused renewal saves
  nothing, so an event raised on the refusal would never be stored. Either a refusal becomes a
  recorded fact (the command succeeds at recording it) or the event is reworked when
  Notifications arrives; until that decision, renewals refuse through the command's own result,
  which is what the desk sees anyway. The courtesy-reminder wording in §7 depends on the same
  decision.

**What building the scheduled process added.** §6 said *five queries, each idempotent* and left the
shape of the memory open. Three things the design did not anticipate:

* **A hold's memory is its own.** The design read as though `RemindersSent` carried the whole run's
  idempotence. It cannot: a hold's deadline has nothing to do with a loan's due date, and one
  memory shared across both cadences would make a renewal forget a pickup warning. The hold keeps
  a flag of its own, reset when a copy is trapped for it — the same copy set aside twice for two
  borrowers is two promises and deserves two warnings.
* **Every stage reads as *at least*.** Written into §6 above. The exact reading looked right until
  the first run that did not happen, at which point the missed message becomes unsendable forever.
* **The fan-out belongs to the host, not to a handler.** Five commands, five scopes, five saves.
  A single command doing all five would make the day one unit of consistency — a hold that could
  not expire would roll back the morning's reminders — and a handler dispatching the other four
  would break the rule against nested dispatch. So the loop lives in the scheduled job, which is
  where "what the day consists of" is a host decision anyway.

One defect the process uncovered, whose reach went well past Circulation: an owned collection whose
key EF believes it generates is a key EF believes already has a row. A reminder recorded against a
loan already on file therefore arrived marked `Modified` rather than `Added`, and — the table being
nothing but its key — no statement was written at all. The same convention silently dropped an
author's credit in Catalog and turned a second hold on an existing queue into a concurrency failure.
Every owned key now says `ValueGeneratedNever`, and a rule over each model holds it.

**What announcing a fact beyond the context added.** Declaring a loan lost now reaches Holdings, and
building that passage — [outbox.md](outbox.md) §9 — settled two things this document had left
implicit.

* **What crosses is flatter than the event.** `LoanDeclaredLost` carries the loan, the copy and the
  borrower; what leaves the context is `CopyReportedLost`, carrying the copy and nothing else.
  Holdings has no use for a borrower, and a contract that offered one would invite it to grow a use.
  Charges will get its own contract from the same event, carrying what Charges needs.
* **The fact names no status.** Circulation reports that a copy is unaccounted for; that this makes
  it `Lost` is Holdings' rule, reached identically by a stocktake that failed to find it. This is
  what keeps the arrow out of the core an announcement rather than an instruction, and §8 of the
  strategic design now says so where the context map records the edge.

Open:

* Does loan history stay in `Loan` forever, or is it archived? It is the only thing in the system
  that grows without bound.
* The replacement charge a declared loss should raise still waits, and now waits only on Charges
  existing — the passage that would carry it is built and proven.
