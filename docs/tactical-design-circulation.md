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
| `OpeningCalendar` | no closure configured | Weekly closed days and dated closures, see below |

One flat set of values. There is no table per member category: a child and an adult may borrow the
same five copies. The policy is still a value object rather than five constants, so indexing it by
category later is an addition rather than a rewrite.

### The calendar

The one policy datum that is not a number: the days the library is open, as the administrator
declares them — weekly closed days, and dated closures. A public holiday and an exceptional
closing arrive identically, as a date, because *why* the door was shut is not a circulation fact.
The holidays are entered, never computed: Easter moves by an arithmetic no library system should
own, two départements keep holidays the rest of France does not, and the set itself changes by
decree — a rule would be wrong somewhere every year, where a list is retyped once a year and wrong
nowhere. The sets name the *closed* days, not the open ones, because that is what the sign on the
door says — and because the empty calendar then means open every day, which is what an
unconfigured host must get: stated the other way round, a host that forgot the setting would run a
library that is never open, and every deadline would slide forever. One weekday must stay open,
held at construction, and it is what guarantees the walk to the first open day terminates.

Two rules consume the calendar, and they are the point of having it.

**A deadline a member must meet never falls on a day they cannot meet it.** The due date — at
checkout and at renewal alike — and a trapped copy's pickup deadline slide to the first open day
on or after where the plain arithmetic lands. Derived once, at the act, from the calendar as it
stands, and the stored date never moves afterwards: the member holds a receipt, and a system that
moves a printed date is the shelfmark mistake transposed from space to time.

**A closed day is never billed.** A fine is charged for days the borrower let pass, and a day
nobody could return is not one of them: `daysLate` counts the *open* days past the due date. The
judgement runs through the calendar as it stands — the stored due date is slid again before
anything is counted, which ordinarily moves nothing and exists for the closure declared after the
act, a strike landing on a due date already printed: the receipt does not move, and the judgement
forgives what the receipt could not know. Due the 24th of December with the library closed through
the 2nd of January, a return on reopening day is billed one open day — the 24th was open, and was
missed — where a calendar-day count would have charged ten and, with `BlockingDebt` at any amount,
cancelled the borrower's holds for the privilege of returning to a locked door.

**What deliberately does not move.** The reminder stages and `DeclaredLostAfter` stay in elapsed
days: a message says how long the copy has been kept, a fine says what the kept days cost, and the
borrower's own calendar agrees with the first — §7 records the two measures where the events carry
them. And the pickup window is slid but not recounted in open days: nothing is billed per day
there, so the only injustice a closure can do is a deadline nobody could meet, which the slide
removes by making the last day one the member can walk in on. A window of open days would
immobilize the copy longer for everyone behind the claim, to protect against a harm that no longer
exists.

One library, one calendar, one owner. Charges multiplies the days it is told and never learns
which days the door was shut — its own §7 records the same line from the other side — and Members
judges a membership against dates no calendar moves. The day a second context needs opening days
is a boundary conversation to have then, not a shared kernel to grow now.

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
`BlockingDebt` and in `HoldCancelledForDebt` and never in anything Charges publishes. Charges
announces `MemberBalanceChanged`, carrying the amount before and the amount after and no opinion
about either; Circulation reads the pair, applies `BlockingDebt`, and forms its own `Debt` and its
own `Standing`. That translation is the anticorruption layer doing its job, and it is what keeps the
rule above from being a slogan.

**The threshold is named on this side only, and that is what makes it movable.** An event announcing
*became owing* would have carried Charges' assumption that the line is zero; moving the line would
then have meant changing what the other context publishes. Reading a balance and judging it here
means the threshold changes in one place — the policy above — and nothing outside this context ever
learns there is one.

### A debt cancels existing holds

When a borrower incurs a debt, their queued holds are cancelled. Not suspended — removed. A borrower
who pays an hour later does not get their place back.

The rule the queues live by follows, and it is stated as what it is:

> **A claim of a borrower who owes money is cancelled as soon as this context learns of it, and
> never served in the meantime.**

A convergence, deliberately. This document once promoted it to an invariant — *nobody in a hold
queue owes money, checkable at any instant* — and the strategic design's own §2 says why that could
never be one: it ties a fact of this context to a fact of Charges, and an invariant spanning two
contexts is not enforced, only hoped for. The cancellation is driven by an event, and between the
fine and the drain's delivery the two facts genuinely disagree — which the boundary test blesses,
since nobody is at the desk when a fine is assessed. What makes the convergence honest rather than
hopeful is three nets, each covering a different failure:

* **At every promotion**, the queue skips borrowers the live balance blocks — skipped, never
  removed, because the debt event may simply not have arrived yet. The one moment a stale queue
  could do damage is the moment a copy is set aside, and that moment always re-asks.
* **On the event's arrival**, the borrower's claims are cancelled — after the handler has confirmed
  against the live balance that the debt still stands (§5): the event is the trigger, never the
  truth.
* **Daily**, the scheduled process sweeps the queues for owing borrowers whose cancellation never
  arrived (§6). A message can die on the drain's floor after its five attempts, the crossing it
  carried is announced only once, and a convergence with no reconciliation is a convergence that
  can silently stop converging: the zombie claim would sit at the head of its queue, skipped at
  every return, never told, occupying one of the borrower's five places forever.

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
  DeclaredLostOn? the day the library stopped waiting — by the clock, or by the borrower's report
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

**A promise can be taken back, and a queue can outlive its edition — both are said out loud.**
When a set-aside copy leaves service before its borrower comes — weeded, water-damaged into
repair, declared lost off the hold shelf — Holdings announces the departure and the claim returns
to the queue at the place its age gives it: the borrower did nothing and loses nothing but the
trip, which the announcement spares them. And when an edition has no copy left in service or
expected back from repair, the daily process ends every claim in its queue: a claim is a promise
of the next available copy, and an edition with nothing left has no next to promise — left alone,
such a queue survives its edition for years, each claim silently holding one of its borrower's
five places. A queued claim has no other expiry: no lifetime attenuates it (§9), and only the
debt, the borrower's own cancellation, or the edition's end can take it.

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
  queue meaningless. *Available* is defined, because it decides whether a member can be stranded
  between the two refusals: a copy is on the shelf when Holdings would lend it **and** no active
  loan carries it **and** no claim has it set aside. The last clause is what keeps the
  single-copy edition honest — the moment its copy is trapped for the first claim, the next
  member may queue behind them rather than being told to fetch a copy that is spoken for.
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

Then: `Loan` is created — due the loan period later, slid off a closed day (§1) — and if this
checkout fulfills a hold, that hold is fulfilled and leaves the queue: the outcome travels in the
event, not in a status the queue keeps.

Step 5 is what stops a walk-in from being handed a copy someone is waiting for.

### Renewal

1. The loan is still active.
2. The borrower is entitled to borrow (query to Members: enrolled, and the membership current).
   A renewal is a fresh loan period, not the tail of an old one, so it opens with the question
   every granting act opens with — added when an audit found the asymmetry nothing had decided:
   an expired membership could no longer borrow, and could renew indefinitely.
3. `RenewalCount` is below `MaxRenewals`.
4. The borrower is in good standing.
5. **Nobody is queued on the edition.**

The fifth is what makes a queue move. Without it a borrower renews indefinitely and the five people
behind them never get anything: the queue exists but does not turn, and a hold stops being a promise.
It reads *queued* deliberately, where this document first said *empty*: a claim already awaiting
pickup has its copy on the hold shelf, and refusing a renewal for its sake would serve nobody the
rule exists to serve. The renewal grants another loan period from the current due date, slid off a
closed day exactly as the checkout's was (§1) — the arithmetic the membership renewal decided, for
the same reason: renewing early costs nothing.

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

1. The `Loan` closes. Lateness is computed and recorded — in open days, against the calendar as
   it stands (§1) — and, when the librarian holding the object says so, the return also records
   that the copy came back spoiled. An input, never a derivation: attributing damage to a loan is
   a desk judgement, and making it at the return is what settles whose loan it was by
   construction. The observation leaves as its own fact for whoever prices damage; what the
   object becomes — worn, rebound, weeded — stays Holdings' record, entered by its own moments.
2. The edition's `HoldQueue` is asked whether the copy is wanted.
3. If it is, the oldest queued hold whose borrower is **in good standing** is trapped: the hold
   becomes `AwaitingPickup`, `TrappedCopyId` is set, `PickupDeadline` starts — the pickup period
   out, slid off a closed day (§1).
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
   the borrower chose — unlike `HoldCancelledForDebt`, which announces a consequence they did not
   choose and always goes out.

No penalty attaches, for the same reasons §9 declines to punish the no-show.

### Declaring a loss

The member says it at the desk — the book is gone, and often in the same breath, that they will
pay for it. This moment is the confession's door, and without one the fact has no entry but the
clock: the loan would run to its thirtieth day of lateness while reminders chase a copy everyone
at the counter already knows is lost, and the money offered there could not be taken.

1. The loan is declared lost — the same terminal end §6's process reaches, by a decision at the
   desk rather than by the calendar, and `DeclaredLostOn` records the day, which is the day the
   lateness stops accruing anything.
2. Everything downstream follows from the one fact, unchanged: Holdings learns the copy is
   unaccounted for, Charges prices the replacement.

Always accepted, like the return: no standing, no cap, no queue — every gate this context keeps
guards what a borrower may *take*, and a loss reported is something given back, if only as a
fact. Refusing a confession would teach members to stop making them. A loan the process already
declared answers success and keeps its first date — whichever of the desk and the clock arrives
second is a redelivery, not a second decision. A returned loan is refused: there is nothing left
to stop waiting for.

### A debt is incurred

Circulation reacts to `MemberBalanceChanged` from Charges, when the balance it carries crosses
`BlockingDebt` from below — a movement that stays on one side of the line does nothing. The pair
the event carries decides whether to wake; the live balance, read from the port before anything
goes, decides whether to act: between the fine and the delivery lies the drain — a minute
ordinarily, longer behind a blocked head — and the most ordinary act at a desk is paying. A
borrower who cleared their debt inside that window keeps their places, because the cancellation is
irreversible by design and a message about a balance that no longer exists is not grounds for it.
When the debt still stands:

1. Every queued hold of that borrower is cancelled.
2. Every hold of theirs awaiting pickup is cancelled, and its trapped copy is released back to the
   queue — where it is offered to the next borrower in good standing.
3. `HoldCancelledForDebt` is published for each claim, so the borrower learns why.

Step 2 matters: a trapped copy for a newly blocked borrower is precisely the waste the rule exists to
prevent, and leaving it on the shelf until its deadline would reintroduce it.

**Ordering, corrected.** This section once claimed the event order kept a copy from being trapped
for a borrower whose fine was in flight — and no ordering of messages could: the trap is
synchronous, inside the return's own command, while the fine crosses two drains. The window is
real and is closed where it can be — every promotion asks the live balance, and the debt handler's
second step releases a copy trapped inside it, at the cost of a trap-and-release nobody sees. What
the drain's strict order genuinely protects is `MemberBalanceChanged` itself: the event carries
the amount before and after, and two movements delivered out of order would make this context
compute a crossing that never happened.

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
| Trapped holds past their deadline | Expire, release the copy, promote the next **in good standing** — the same net every promotion casts |
| Borrowers owing money who still hold live claims | Cancel them — the reconciliation behind the debt event (§3) |
| Queues whose edition has nothing in service or expected back | Cancel every claim, each out loud — the promise can no longer be kept (§4) |

The debt row is a reconciliation and never the first line of defence: the debt event does that
work the day it arrives, and on any day every event arrived — which is every ordinary day — the
sweep finds nothing. It runs before the two hold rows, so a claim a lost message left standing is
neither warned about nor expired as if it were honest.

The third row is the second road to its fact, not the only one: a borrower who reports the loss
at the desk does not wait thirty days for the system to agree (§5), and the run declares only
what nobody confessed.

**Every stage reads as *at least*, not exactly.** A run that did not happen for three days finds a
loan nine days late with neither the first nor the seventh stage announced. It marks both spent and
sends one message, naming the real nine days. On an exact reading the missed stages would be owed to
the borrower forever and never sent; announcing each of them in turn would deliver three messages in
one morning, which is the noise the schedule exists to avoid. The same reading gives the courtesy
reminder its window rather than its day, and the imminent-expiry warning the two days above:
a deadline is a date the run must not step over, and a run only ever fires once a day.

The run keeps no desk hours: it fires on closed days too, and the stages it measures stay in
elapsed days on purpose — §1 records the split, a message says how long where a fine says what it
costs. The one calendar-aware judgement here is the pickup deadline: a claim expires only once the
first open day on or after its deadline has passed, so a closure declared after a copy was set
aside does not cost a borrower their claim for not walking through a locked door. The imminent-
expiry warning keeps reading the stored date — warning a day early in that rare case is noise,
expiring a day early would be a broken promise.

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
| `RenewalGranted(…, newDueDate)` | Notifications, read model |
| `HoldPlaced` | read model |
| `HoldFulfilled` | read model |
| `HoldReadyForPickup(…, pickupDeadline)` | Notifications |
| `HoldExpiringSoon` | Notifications |
| `HoldExpired` | Notifications |
| `HoldCancelled` | read model, Notifications |
| `HoldCancelledForDebt` | Notifications |
| `HoldPickupWithdrawn` | Notifications, read model |
| `HoldCancelledUnfulfillable` | Notifications, read model |
| `LoanRecovered(…, daysLate)` | Charges, read model |
| `CopyReturnedDamaged` | Charges, read model |

This table once listed `LoanRenewed` beside `RenewalGranted`. They were one fact under two names —
a renewal succeeded and the due date moved — and a reader had no way to tell which to subscribe to.
`RenewalGranted` is the one, because it names the outcome and carries the new date; nothing was ever
published under the other name.

`HoldCancelledForDebt` is **singular**, and this table said `HoldsCancelledForDebt` until the code
settled it. The plural did not survive the aggregate boundary: a borrower's claims live in as many
queues as there are editions, an event is raised by the aggregate whose state changed, and no queue
spans the others. So one debt cancelling four holds raises four events, not one carrying four. What
a borrower reads should still be a single message, and grouping them into one is Notifications'
work — which is where a fact about messages belongs, and not a reason to invent an aggregate that
would exist only to hold the plural. `docs/tactical-design-charges.md` §10 records the same finding
from the side that asked for it.

`LoanBecameOverdue` carries the days actually elapsed and not the stage that fired it. A borrower
told they are nine days late can act; one told they have reached "stage two" cannot, and after a run
that missed a day or two the stage is no longer even true.

`LoanReturned` carries `daysLate` and not a price. Whether a return was late is a circulation fact;
what lateness costs is a money question, and Charges answers it. A `daysLate` of zero is published
all the same — Charges decides there is nothing to charge.

The two events therefore carry two measures of one lateness, and the difference is deliberate
rather than drift: `daysOverdue` is elapsed days, because a message says how long the copy has
been kept and the borrower's own calendar must agree with it; `daysLate` is open days (§1),
because a fine bills what the borrower could have done and a closed day is not that. Folding them
into one number would make either the message lie or the fine unfair, and which one would depend
on which was folded into which.

**A refusal must say which of the three it is** — the limit is reached, the borrower owes money,
someone is waiting — because they call for three different things from the borrower. "We could not
renew your loan" without saying which is a message that wastes a trip to the library.

That requirement is met by the command's own result and not by an event. The three are distinct
error codes, the librarian reads which one on the screen, and the borrower is standing there to be
told. This table listed a `RenewalRefused` for Notifications until the requirement was looked at
squarely: there is no self-service in this system, so a refusal has no remote audience, and §10
records why the event is gone rather than pending.

The courtesy reminder is where a borrower is spared the trip in the first place. Three days before a
due date on an edition with a queue, the useful message is not *"renew it"* but *"someone is
waiting, please bring it back"* — which is why `LoanDueSoon` carries `anyoneIsWaiting`, and why it
does the work a refusal notice would only ever have done too late.

**Consumed.**

| Contract | From | Effect |
|---|---|---|
| `MemberBalanceChanged(…, previousBalance, currentBalance)` | Charges | Cancel the borrower's holds, when the pair crosses `BlockingDebt` upwards and the live balance confirms it. A movement that crosses nothing, and a return to good standing, both change nothing in the model — the borrower is simply able to act again |
| `CopyLeftService` | Holdings | Release the promise, if a claim had the copy set aside: the claim returns to the head of its queue and the withdrawal of the pickup is announced (§4) |
| `CopyRecovered` | Holdings | Settle the written-off loan the copy concerns: the lateness frozen at `DeclaredLostOn` is announced at last, through the same contract an ordinary return uses |

The column says *contract* for the reason Charges' own table gives: what crosses is a record of
primitives in the publisher's published language, and the domain events behind them are types this
context may not name.

`LoanRecovered` is what closes the arithmetic the write-off left open. Without it, a copy brought
back on day forty-five cost less than one brought back on day twenty-nine — the replacement charge
cancelled by the find, the fine never assessed because the loan ended without a return. The
lateness it announces froze the day the library stopped waiting: a recovered loan owes exactly
what a return on the write-off day would have owed, and the years behind a radiator are nobody's
fine. The loan itself stays `DeclaredLost` — the statistics that count completed loans gain
nothing from a resurfacing.

A third flattening leaves this context on every return: `CopyReturned`, carrying the copy and
nothing else, for whoever keeps the stock — an object over the desk cannot be unaccounted for,
and Holdings must be able to agree with the shelf without a human remembering to tell it. Almost
every delivery is a no-op on the reader's side, the price `MemberBalanceChanged` already pays in
the other cycle, for the same reason: deciding here which returns matter would mean knowing
another module's statuses.

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

**A queued claim has no lifetime.** Some systems expire a hold not served within six months or a
year, to keep the cap's places moving. Decided against, by the product owner: the patient reader
of a much-demanded title is exactly who a queue exists to serve, and their patience is not a fault
to correct. What actually strands a claim forever is an edition that can no longer serve it, and
that is handled where the cause is — the unfulfillability sweep (§6) ends the whole queue, out
loud, the day nothing is left to promise. The trigger to revisit: queues on *living* editions
turning so slowly that places sit occupied for a year — measurable from the history projection,
like the no-show, before any rule is written.

## 10. Consequences and open questions

**What building the desk moments added.** The five synchronous moments — checkout, return,
renewal, placing and cancelling a hold — are implemented, and so is §6's scheduled process and the
first fact this context announces beyond itself. Charges has since been built, and with it the one
reaction this context owed the other direction: `CancelHoldsWhenDebtBegins` subscribes to
`MemberBalanceChanged`, applies `BlockingDebt` to the pair of amounts, and dispatches this module's
own `CancelHoldsForDebtCommand` — so nothing on this list waits on another module any more.

Building the desk taught four things this document now carries in place: the entitlement
precondition the checkout list omitted, the cap moving after the trap resolution, the renewal rule
reading *queued* rather than *empty*, and `HoldFulfilled` joining the events table — §4 promised
every outcome an event, and the table had skipped the happiest one.

Two things the design did not anticipate, settled by the code:

* **The loan records its `EditionId`.** The return and every renewal must name the queue the copy
  answers to, and the loan is the only thing at hand that can remember it. Holdings' lendability
  answer now carries the edition — the Customer/Supplier contract change the context map priced
  in advance — and the loan keeps it from checkout on, as the queue the copy played in whatever
  Catalog later does to the edition.
* **`RenewalRefused` could not be published as designed, and turned out not to be wanted.** The
  pipeline writes the outbox in the same save as the change and saves only when the command
  succeeds, so an event raised on a refusal would never be stored. That was read as a mechanical
  obstacle for two phases; looking at what the event was *for* dissolved it. Its only consumer was
  Notifications, and a renewal is refused at the desk with the borrower standing there — Members §9
  settles that there is no self-service, so no refusal has a remote audience. The requirement §7
  actually stated, that a refusal say which of the three reasons it is, is met by the command's
  result carrying `RenewalLimitReached`, `DebtForbidsIt` or `SomeoneIsWaiting`; and the borrower-
  facing half was solved better elsewhere, by `LoanDueSoon` carrying `anyoneIsWaiting` three days
  ahead, which spares the trip a refusal notice could only ever have reported afterwards. The event
  is removed rather than deferred. What is genuinely lost is measurement — §5 calls the queue rule
  blunt on purpose, and how often it refuses is exactly what one would want counted before
  softening it — and the logging behavior, outermost so that refusals still leave a line, is what
  answers that until somebody asks for it properly.

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

**What designing Charges settled here.** This document long said Charges would announce
`MemberBalanceBecameOwing` and `MemberBalanceSettled`, and writing the other side showed the pair to
be lacunary rather than merely verbose. They report two crossings, both of zero, which is complete
for `BlockingDebt` as it stands and for no other value of it: a balance moving from twenty cents to
twelve euros crosses a ten-euro threshold and announces nothing. The rule would stop firing with no
error and no failing test — and §3 already records, in its own words, why the threshold is likely to
move one day.

So Charges announces `MemberBalanceChanged`, carrying the amount before and after, and the crossing
is computed here. The threshold is named on this side only, which is what makes it movable without
touching what another context publishes. `MemberBalanceSettled` is not replaced by anything, because
§7 had already recorded that it changed nothing in this model.

**What the calendar added.** §1's opening calendar arrived after the desk moments were built —
the product owner's decision that a closed day and a public holiday must reach the due dates and
the fines — and building it settled four things the decision alone had not:

* **The receipt does not move; the judgement re-slides.** The stored due date and pickup deadline
  are derived once, at the act, and a calendar changed afterwards leaves them standing — but every
  judgement over them (the billable count, the expiry) first slides the stored date through the
  calendar *as it now stands*. Ordinarily that moves nothing; on the day a strike lands on a date
  already printed, it is the difference between forgiving the closure and fining people for it.
* **The sets name the closed days**, so the empty calendar means open every day — which is the
  behavior every existing test already asserted, and is what let the calendar arrive without
  moving a single green test. The opposite polarity would have made an unconfigured host a library
  that is never open, failing by sliding every deadline forever.
* **At least one weekday stays open**, held at construction rather than trusted: the walk to the
  first open day must terminate, and the guard is on the weekly pattern alone because the dated
  closures are finitely many — past the last of them, only the pattern closes anything.
* **The pickup window slides and is not recounted** in open days, where the fine is. The asymmetry
  is argued in §1, and it fell out of asking what each number is *for*: the fine bills per day, the
  window only promises a last day the member can actually use.

**What the recovered copy and the dying queue added.** The map's second cycle — Holdings
announcing, this context deciding — arrived with three lessons worth keeping. The recovery's
arithmetic needed a date nobody had thought to store: `DeclaredLostOn`, recorded by both roads to
the write-off, because the fine a resurfacing settles froze the day the library stopped waiting.
The released promise needed no new position rule: a claim keeps `PlacedOn`, so releasing it puts
it back at the head by construction — the ordering invariant did the work. And the unfulfillable
queue needed the port to answer about the future, not the present: *lendable now* counts a copy
in repair as no, and a queue waiting on a rebinding waits on something real, so the port grew
*expected to serve* rather than this context growing an opinion about repair.

**What publishing the borrower's file added.** `GetBorrowerFileQuery` answers the circulation half
of the desk's member file — the loans, the claims and the standing — and it is one question rather
than two on purpose: loans and holds are in the same context precisely because a copy shelved that
was promised must be visible at the desk in the same breath as the return that freed it, and two
queries would let a page show one without the other. Three things it settled.

* **`Standing` is exported, and stays a single judgement.** The read side lives in this module's
  infrastructure, a second assembly, so the file's handler could not reach the internal helper. The
  alternative was two lines in the handler — fetch the amount, apply `DebtForbids` — and the reason
  that is worse than a widened surface is the direction the copies would drift: the day a debt stops
  being the only thing that blocks a borrower, the desk paths and the screen would disagree about
  the same person, and only one of them would be tested. `Standing.IsBlockedAsync` is public;
  judging a whole queue stays internal, because that is desk machinery and not something a file
  displays.
* **The balance is read twice while the page is built** — here to judge, and by Charges to display.
  Deliberate, and the strategic design's §10 is why: the composer holds the amount already, and
  deriving the verdict there would put a rule where no module's invariants cover it. Two reads of a
  handful of rows is what the glossary's seam costs.
* **A returned loan stays off the file.** It answers what a borrower must still answer for — on
  loan, or declared lost and unresolved. The whole history is the open question below, and putting
  it behind a desk screen would have answered that question by accident, in the direction hardest
  to undo.

One mechanical lesson, recorded because it cost a rewrite and nothing would have caught it before
production: a borrower's claims live in as many queues as there are editions, and the natural way
to read them — filtering the holds *inside* the collection selector — compiles, reads better, and
does not translate. Pairing the queue with its holds first, then filtering, produces the join the
borrower index was built for. Query shape is not provable by the compiler, and this is the argument
for the integration tests over each published query.

Open:

* Does loan history stay in `Loan` forever, or is it archived? It is the only thing in the system
  that grows without bound. The borrower's file now has a stake in the answer: it excludes returned
  loans, so the day history is asked for at the desk it is a second question and not a wider one.
