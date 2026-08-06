# Tactical design — Holdings

What *this* library owns. A bibliographic record is shared with every library in the world; a copy
with a barcode and a worn spine is ours alone, and this context is everything true of that object
between the day it arrives and the day it leaves.

Boundaries come from [strategic-design.md](strategic-design.md). This document decides aggregates,
invariants and the moments where they meet.

## 1. What the business can change

Holdings has no policy object. Circulation needs one because its numbers — how many, how long, how
often — are decisions a library revises without a developer; nothing here is of that kind. Two
formats are the only settings, and both are conventions rather than levers:

| Setting | Value | Note |
|---|---|---|
| `Barcode` length | 4 to 32 characters | A range, not a format, see §3 |
| `Shelfmark` length | up to 64 characters | Long enough for a section prefix and a class number |

Naming a policy object for two string bounds would be ceremony. The day a rule appears that staff
actually argue about — which sections may be lent, how long a repair may run — it earns one.

## 2. The aggregate

One aggregate, and one is the right number: nothing in this context spans two copies. Weeding a
shelf of forty is forty independent decisions that happen to be taken in one afternoon.

```
Copy
  CopyId          identity
  EditionId       from Catalog — an identifier, never the edition
  Barcode         unique in the library, see §3
  Shelfmark       where this copy stands, see §4
  Condition       Good | Worn | Damaged
  Status          InService | InRepair | ReferenceOnly | Withdrawn | Lost
  ReturnsTo       what a repair ends in, see §5
  AcquiredOn
```

**Invariants.**

* `EditionId` never changes except by an explicit correction, and never becomes null.
* `Barcode` and `Shelfmark` are never blank.
* `Withdrawn` is terminal.
* A copy in repair remembers the status it returns to, and that status is never `InRepair`.

**`Status` and `Condition` are two axes, not one.** A worn copy is perfectly lendable and most of a
public library's stock is worn; a copy in pristine condition may be reference-only because it is the
only one. Merging them into a single enum would force a choice between recording what the object
*is* and recording what may be *done* with it, and a librarian needs both. The distinction is not an
invention: the systems the profession actually uses keep damage, lending restriction and withdrawal
as separate fields for this exact reason.

`Condition` decides nothing today. It is recorded because staff record it, and because the decision
to repair or to weed is argued from it. The day it starts blocking a loan it becomes a second input
to §6, and that is a rule to write when someone asks for it.

**Status is not availability.** Holdings knows a copy is in repair, reference-only, lost or
withdrawn. It does not know it is out on loan, and must not learn: availability is the conjunction
of a Holdings fact and a Circulation fact, computed by whoever asks and stored in neither.

That is also why the fifth value is called `InService` and not `OnShelf`. A copy someone borrowed
last Tuesday is not on a shelf, and Holdings has no way to know that it isn't — a status naming a
physical location would be false for a large share of the stock at any moment, and *silently* false,
which is worse. `InService` claims only what this context can actually vouch for: nothing about this
copy prevents it from being lent. The glossary row was amended to match when the module was built.

## 3. `Barcode`

The label a copy is identified by at the desk. Unique in the library.

**It is not the identity.** `CopyId` is. A label peels off, tears, or stops scanning, and it is
replaced — a routine afternoon's work at a service desk. Were the barcode the identity, re-labelling
would produce a different copy, and every loan ever made against the old one would point at nothing.
The barcode is a *current* fact about the object, and `Relabel` is an ordinary operation.

**Uniqueness is a set rule, not an invariant.** A `Copy` cannot see the other copies, so it cannot
enforce it — the same shape as the cap in Circulation, and it gets the same treatment: the handler
asks, and a unique index in the module's schema is what actually holds. The handler's check exists
for the message, not for the guarantee: "barcode 30124 is already on another copy" is something a
constraint violation cannot say.

**The model does not parse it.** Libraries print barcodes in pre-bought ranges, in whichever symbology
their scanners were sold with — Codabar, EAN-13, Code 39 — and the same shelf can carry two
generations of them. A value object validating a format would reject a legitimate label the day a
new batch arrives, and the failure would surface at the desk with a reader waiting. Length and
non-blankness are the whole of it.

## 4. `Shelfmark`

Where *this copy* stands on the shelves. Per copy, never per edition: one copy of a title lives in
the children's section and another in the reserve, and that is the ordinary case, not an edge one.

**Built from Catalog's facts, owned here, and derived once.** A French public library composes it
from the class number and the first letters of the author's preferred name — `843.912 SAI` for
Saint-Exupéry — often behind a section prefix, `JEUN 843.912 SAI`.

The important word is *once*. A shelfmark is not a computed view of Catalog data; it is a decision
taken when the copy is processed, printed on a label, and stuck to a spine. When a cataloger later
corrects an author's preferred name, **the shelfmarks do not move** — the labels on the shelves have
not moved, and a system claiming otherwise would send staff to the wrong shelf. Modeling it as a
derived property would be the natural mistake and would be wrong in the only way that matters, which
is physically.

Relabelling a shelf after a reclassification is real work a library schedules, and it goes through
`Reshelve` — a decision, deliberately, not a propagation.

## 5. Repair remembers where it came from

`InRepair` is the only status that has to be undone, and undoing it is where a naive model breaks: a
reference-only copy sent for rebinding must come back reference-only. Returning everything to
`InService` would quietly release the library's only copy of something into the lending stock, and
nobody would notice until it left the building.

Hence `ReturnsTo`, set when the repair starts and consumed when it ends. It holds `InService` or
`ReferenceOnly` and never anything else: a copy cannot be sent for repair from `Withdrawn`, which is
terminal, nor from `Lost`, which must be found first.

A single boolean would not do. There are two states a repair can return to today and the set is open
— a future *on display* or *reserved for a reading room* would be a third — so the field records the
status rather than a flag standing in for one.

## 6. Lendability is the one question Circulation asks

The context map makes Holdings the supplier and Circulation the customer, and the contract is one
question: **may this copy be lent?** It is answered synchronously, at the desk, with a member
standing there, and the answer is a fact about this context alone.

```
InService      → yes
InRepair       → no
ReferenceOnly  → no
Withdrawn      → no
Lost           → no
```

**It answers with lendability and never with availability.** Whether the copy is already on loan is
Circulation's own fact, and Circulation checks it immediately afterwards — its checkout
preconditions place the two on consecutive lines. Holdings answering "available" would mean asserting
something it cannot see, and would put half of Circulation's rule in the wrong context.

This is an Open Host Service in the proper sense, unlike the balance query in the other direction:
the protocol is published by the upstream, for anyone who needs it. A read model will ask the same
question, and so will the staff interface.

**A copy that does not exist is not a "no".** It is a different answer, and the port says so — a
barcode that matches nothing means a mis-scan or a copy never accessioned, and a librarian handles
those two differently from a copy that is simply in repair.

## 7. The moments

### Acquire

A copy enters the collection. Preconditions, cheapest first:

1. The barcode is not already on another copy (§3).
2. The edition exists.

The second crosses a context boundary, and it is the first time this codebase has had to. Catalog
publishes an edition's existence and a summary — "identifier plus summary is all that crosses" — and
Holdings holds the `EditionId` and nothing else. It is the same shape as the rule Catalog already
enforces when a work credits an author: a question spanning two aggregates that neither can answer
alone, so the handler asks it, and it reports *which* edition is missing where a foreign key would
report only that something was.

**No foreign key backs that reference**, by the rule in the strategic design: an integrity constraint
across schemas is a coupling the compiler cannot see. The consequence is stated plainly in §10.

The copy arrives `InService`, or `ReferenceOnly` when the acquisition decision says so. `Condition`
starts at `Good` unless a damaged delivery says otherwise.

### Reshelve, Relabel, Recondition

Three ordinary corrections, each a fact about the object rather than about the record: a copy moves
section, a label is replaced, a spine is recorded as worn. None has a precondition beyond the
aggregate not being `Withdrawn` — there is nothing to reshelve once the copy has left.

### Send for repair, Return from repair

`ReturnsTo` is set on the way in and consumed on the way out (§5). A copy already `InRepair` cannot
be sent again; a copy not `InRepair` cannot come back.

### Restrict to reference, Release for lending

A curatorial decision, taken and reversed by staff. The only transition worth guarding is that
neither may be applied to a copy in repair — the pair belongs to `ReturnsTo` at that point, and
changing the destination mid-repair is a different operation nobody has asked for.

### Withdraw

*Désherber.* The copy leaves the collection on purpose, and the state is terminal: a withdrawal is
recorded because it happened, and a copy that came back would be an accession, not an undo.

**Nothing checks that it is not on loan**, and that is deliberate rather than an omission. Weeding is
a shelf operation: the librarian is holding the object. The rule is satisfied physically, and
enforcing it would mean Holdings asking Circulation a question — reversing the supplier relationship
for a case that cannot arise at the desk. Should a bulk weeding tool ever act on a list rather than
on a trolley, that tool asks the question, not the aggregate.

### Declare lost, and find again

Two ways in, and this is the one place the context reacts to another.

* A **stocktake** does not find the copy (§8).
* **Circulation** declares a loan lost after thirty days, and publishes it. Holdings marks the copy
  `Lost`, which is exactly the line the strategic design's stress table gives this context: it leaves
  the lendable stock, and it is **not withdrawn**, because nobody decided to part with it.

**`Lost` is not terminal, and that is the difference from `Withdrawn`.** Copies turn up — reshelved
two rows down, returned in a book drop months later, found behind a radiator during a récolement.
Without `Find`, the only route out of `Lost` is someone editing the database, which is how a model
teaches its users to distrust it.

Note the symmetry with Circulation, and the reason the words differ. A *loan* that is `DeclaredLost`
is terminal: the library decided to stop waiting, and finding the copy afterwards does not reopen the
loan — it starts a return. A *copy* that is `Lost` is a state of the world, and the world changes.
One is a decision, the other an observation, which is why they are two words.

## 8. Stocktake, deliberately deferred

*Récolement*: the physical sweep of the shelves against the records, which a library runs section by
section over weeks. It is named in the glossary and it is not modeled here.

It needs concepts this context does not yet have — a session with a scope and a date, a scanning
pass, a reconciliation report — and every one of them is shaped by the interface staff will actually
hold, which does not exist. Modeling it now would be guessing at a workflow.

One thing it forces today, though, and the reason it is discussed at all: **`Lost` must be reachable
from Holdings' own initiative**, not only from Circulation's event. Were the only route in the
event, the day a stocktake lands it would find the context unable to record its own principal
finding. §7 provides that route now, at no cost.

## 9. Events

Holdings publishes facts about objects. It does not know that Circulation exists, and the one thing
Circulation needs from it is a question, not an announcement (§6).

**Published.**

| Event | Consumed by |
|---|---|
| `CopyAcquired(copyId, editionId, barcode)` | read model |
| `CopyReshelved(…, previousShelfmark, newShelfmark)` | read model |
| `CopyConditionRecorded(…, previousCondition, newCondition)` | read model |
| `CopyRelabelled(…, previousBarcode, newBarcode)` | read model |
| `CopySentForRepair`, `CopyReturnedFromRepair` | read model |
| `CopyRestrictedToReference`, `CopyReleasedForLending` | read model |
| `CopyDeclaredLost`, `CopyFound` | read model |
| `CopyWithdrawn` | read model |

Every one of them feeds a projection and nothing else, today. That is not a reason to withhold them:
the stock ledger a librarian consults — how many copies of this edition, where, in what state — is a
read model fed by exactly this stream, and an event not published when it happened cannot be
recovered afterwards.

`CopyRelabelled` carries the previous barcode because a projection keyed on the label has to retract
the old one, the same shape as a corrected preferred name in Catalog.

**Consumed.**

| Contract | From | Effect |
|---|---|---|
| `CopyReportedLost` | Circulation | The copy becomes `Lost` |

**What arrives is the contract, never the domain event.** Circulation raises `LoanDeclaredLost`
internally; what crosses the boundary is `CopyReportedLost`, a record of primitives in Circulation's
published language, carrying the copy and nothing else. This module could not name the domain event
even if it wanted to — it may not reference another module's `Domain`, and an architecture rule
refuses a subscriber that names anything but a published language. The column heading says
*contract* for that reason: the two are different types with different audiences, and a table
listing the event here would describe a coupling this module is forbidden to have.

Handled the way every event is handled here: later, in a transaction of its own, idempotently, keyed
by `EventId`. Marking a copy lost twice is marking it lost once, so idempotence costs nothing — and
the subscriber does not write, it dispatches this module's own `DeclareCopyLostCommand`, so the
change lands in this module's transaction rather than in the drain's ([outbox.md](outbox.md) §9).

**A return changes nothing in Holdings.** When a returned copy is set aside for the first hold, it is
the *hold* that records the trapped copy, in Circulation. Holdings would be recording a circulation
fact about a physical object, which is the boundary error this context exists to avoid.

## 10. Consequences and open questions

**The amendment this document asked for, and got.** The Holdings glossary row `OnShelf | En rayon`
is now `InService | En service`, for the reason in §2: a librarian says *en rayon* about a copy that
is physically there, and Holdings cannot know that.

**What building it added.** Two things the design did not anticipate and the code settled.

A copy's status has to be *stored* as its name rather than its number, for the reason the access
point index already stored its kind that way: this table is read by a human when something looks
wrong, and `InRepair` answers where `1` asks.

And one database holding two modules' schemas does not compose for free. `EnsureCreated` builds the
database and then answers "already there" for the second context over it, leaving that module's
tables unbuilt — the relational creator has to be asked for them directly. It is a test's problem
today and a host's problem tomorrow, and the answer there is migrations.

**A merged edition orphans this context's identifiers.** Catalog has no merge operation, and the day
it acquires one, every `Copy` holding the absorbed `EditionId` points at a record that no longer
answers. No database will report it: referencing across schemas by identifier is the right call and
it is precisely what removes that net. Whatever merge Catalog eventually publishes has to be an
event this context consumes, and Holdings is the first module that makes the gap concrete rather
than theoretical.

Open, and each deferred for a stated reason rather than forgotten:

* Whether a mis-cataloged copy is corrected in place or withdrawn and re-accessioned. Catalog's own
  distinction is the model — `Rename` records a fact about the person, `CorrectPreferredName` a fact
  about the record — and a copy attached to the wrong edition is squarely the second kind. Leaning
  towards a `CorrectEdition` operation that keeps the barcode and the loan history, since the object
  on the shelf never changed.
* Whether `Condition` ever blocks a loan (§2).
* Whether a section is part of the shelfmark string or a field of its own. It is a prefix today; it
  becomes a field the day someone wants to count the children's collection, and that is a report
  nobody has asked for.
* Provenance — donor, price, invoice. Acquisitions is out of scope by assumption, and these are its
  facts, not ours.
* Multiple locations. The single-library assumption holds; a network of branches would make
  `Shelfmark` a pair of branch and shelfmark, and would give this context transfers, which is a
  whole moment it does not have.
