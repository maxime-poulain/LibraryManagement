# ADR-0017 — A merge is an event, and Circulation is what it costs

- **Status**: Accepted
- **Date**: 2026-08-10
- **Authority**: [`tactical-design-catalog.md`](../tactical-design-catalog.md) §10,
  [`strategic-design.md`](../strategic-design.md) §8 and §11, [`outbox.md`](../outbox.md) §9

## Context

Three documents already name the same gap. Catalog §10 calls merging *"the gap every client has
already named"* and settles the one rule that was obvious: whatever Catalog publishes must be an
**event its downstream consumes**, never an update, because no database constraint crosses a schema
and nothing else can carry the news. Holdings §10 records the orphaned `EditionId`. Members §10
records the same shape for duplicate members.

What none of them settles is what each consumer actually *does* when the event arrives. And one
consumer was never named at all.

**Circulation is the most exposed context, and §11 leaves it out.** That list says the merge is
*"the event their identifiers wait on"* for **Holdings and Members**. But the three downstream
contexts do not hold the identifier the same way:

| Context | How it holds the identifier | What a merge costs |
|---|---|---|
| Holdings | `Copy.EditionId`, a field | repoint a column |
| Charges | `MemberId`, an account key | nothing, or repoint |
| **Circulation** | **`HoldQueue` is an aggregate _keyed_ by `EditionId`** | **merge two aggregates** |

`HoldQueue : AggregateRoot<EditionId>`. Merging two editions does not repoint a field: two queues
must become one, with the `PlacedOn` order recomposed across both.

And that collides with an invariant the aggregate holds on purpose — *a borrower appears at most
once in a queue*, refused in `PlaceHold`. Two queues combined can hold the same borrower twice, so
a naive merge would manufacture a state the aggregate refuses to create.

**The failure is silent, which is what makes it worth an ADR rather than a comment.** `CancelFor`
finds a borrower's claim with `FirstOrDefault`. Given two, it removes an arbitrary one, raises
`HoldCancelled` for it, and returns success — the desk cancels a hold, tells the borrower it is
done, and the borrower is still in the queue. Nothing throws and nothing logs.

**The same wall, reached from the other side.** Merging two *members* combines the claims of two
`BorrowerId`s into the same queues and hits exactly the same invariant. The two merges the
documents treat as separate subjects fail in the same place.

## Decision

### What crosses

Two flat contracts of primitives, each in its publisher's own published language:

- Catalog publishes `EditionsMerged(absorbedEditionId, survivingEditionId)`.
- Members publishes `MembersMerged(absorbedMemberId, survivingMemberId)`.

Two identifiers and nothing else. The absorbed record's title, its name, its reason for being
merged — none of it is any consumer's business, and a contract that offered them would invite a
consumer to grow a use for them.

Catalog's *authority* merge does not cross at all: no module outside Catalog holds an `AuthorId`.
It is an internal change to `Work.AuthorIds` and the access-point index, and it is the easy half
precisely because the boundary already contains it.

### What each consumer owes

Each subscriber translates the contract into a **command of its own module and dispatches it**
(`outbox.md` §9) — never a write of its own, because the drain saves the announcing module's
context.

- **Holdings** repoints every `Copy` holding the absorbed `EditionId`.
- **Circulation** repoints `Loan.EditionId`, and merges the queues.
- **Charges** does nothing for an edition. For a member, the surviving account absorbs the other's
  outstanding charges — and that is Charges' own rule to write when it gets there, not this
  record's.

### The queue merge, which is the hard part

The merged queue is the union of both queues' holds, ordered by `PlacedOn` — which is already how
order works, so the ordering itself needs no new rule. Two rules are needed for the collisions:

1. **A claim awaiting pickup always survives.** A copy is physically on the hold shelf with
   somebody's name on it. Dropping such a claim strands the copy and breaks a promise the library
   already made in the world.
2. **Among a borrower's *queued* claims, only the earliest survives.** Their claim on the work is
   as old as the first time they asked for it; the later one was a claim on what turns out to be
   the same thing.

So the invariant is restated rather than abandoned: **at most one queued claim per borrower.** It
never had to speak about claims already promised a copy, and the merge is what reveals that it was
overstated — a borrower may legitimately end up holding one queued claim and one copy awaiting
pickup, or in the rare double-trap, two copies set aside. The database agrees: the unique index on
`TrappedCopyId` is filtered and spans queues, so two *different* trapped copies in one queue
violate nothing.

**`CancelFor` must stop assuming.** Once a borrower can hold two claims in one queue, choosing one
by `FirstOrDefault` is the silent bug described above. The desk act "cancel my hold" must name
which hold — the command grows a `HoldId` — and the aggregate refuses rather than guesses.

### What a merge does not do

**It does not rewrite history.** A loan that named the absorbed edition happened, and saying
otherwise would falsify the past to make the present tidy. Only live state moves: copies, active
loans, queued claims. Returned loans keep the identifier they were made under.

### The order to build it

1. Catalog's merge operation and its event — the fact becomes recordable.
2. Holdings' subscriber — the simplest consumer, and it proves the passage.
3. Circulation's `Loan.EditionId` repointing.
4. The queue merge, with the invariant restated and `CancelFor` tightened.
5. Members' merge and its own event, which reuses steps 3 and 4's machinery.

Four modules, so at least four pull requests. Deciding the shape here is what lets each of them be
reviewed on its own.

## Consequences

The strategic design's §11 sentence is wrong as written and this record is the reason it changes:
Circulation waits on this event too, and it waits hardest.

Circulation's tactical design gains a merge section. It had none, which is how a context that keys
an aggregate by a foreign identifier came to be absent from the list of contexts a merge affects.

An invariant loosens, and the loosening is written down with its reason rather than discovered
later by someone reading `PlaceHold` and wondering why the queue holds a borrower twice.

The merge stays unbuilt. What changes today is that it is decided, and that the next person to
start it knows Circulation is the expensive part rather than finding out in the middle.

**Progress against the build order**, kept here because a record whose consequences read as future
tense long after the fact misleads whoever reads it next:

| Step | State |
|---|---|
| 1. Catalog's merge operation and its event | Built |
| 2. Holdings' subscriber | Built — the passage is proved |
| 3. Circulation's `Loan.EditionId` repointing | Built — live loans only |
| 4. The queue merge, and `CancelFor` tightened | Built |
| 5. Members' merge and its own event | Built — the announcing half; no consumer yet |

Nothing in the decision above changed while steps 1 to 5 were built. What they did not anticipate is
recorded where it belongs rather than here: that all four merge rules want to live together in a
domain service (`tactical-design-catalog.md` §10); that the consuming side's three tempting guards —
the copy's status, the survivor's existence, a deduplication table — are all wrong
(`tactical-design-holdings.md` §10); and that *"only live state moves"* cuts differently in each
consumer, since a copy's edition is asked about forever while a finished loan's is asked about never
(`tactical-design-circulation.md` §10).

One thing this record did not foresee at all, and step 4 answered: **nothing stopped a hold being
placed on an absorbed edition.** A consumer learns that two records merged, never that one is
absorbed, so a fresh claim on the absorbed identifier built a queue no return would feed once the
loans named the survivor. Placing a hold now asks Catalog whether the identifier still names a
record — the port already answers no for an absorbed one, which is why this needed no new state. What
it still cannot do is name the survivor in the refusal, and that is Catalog's port to widen if anyone
ever needs it (`tactical-design-circulation.md` §10).

**Members' half went in last and cost the least**, which the build order predicted: the same shape
as step 1, and the guard against new loans and holds attaching to an absorbed identifier was one
clause in a port Circulation already calls. What it added is recorded in Members' §10 — that the two
terminal states this context now has are not interchangeable, and that erasure has to stay reachable
*through* a merge, because a merge does not anonymize and a person's right does not stop at the file
somebody judged secondary.

What remains after it is the consuming half of *this* merge: Circulation repointing live loans and
combining a borrower's claims across queues, and Charges having the surviving account absorb the
other's outstanding charges. Both reuse machinery that now exists, which is what step 5 was placed
last to be able to say.

**The queue merge went in as decided**, both rules unchanged. What building it added is recorded in
Circulation's §10 rather than here, and the piece worth reading from this record's perspective is
that the loosened invariant did not loosen the desk: `PlaceHold` still refuses a borrower any second
live claim, and only a merge reaches the state the invariant now permits. A guarantee and a refusal
are different lines, and this record's *"the invariant is restated rather than abandoned"* was one
sentence short of saying so.

## Alternatives rejected

**Refusing the merge while any copy is awaiting pickup.** Clean, and it makes the invariant
survive untouched. It also blocks a cataloging correction on a borrower who does not come to the
desk — the pickup window is days long, the librarian holding two records for one edition cannot
wait, and "fix your catalog when nobody has a book on the shelf" is a rule that will simply be
worked around.

**Cancelling the later claim outright, awaiting pickup or not.** Simple to implement and it keeps
the invariant exactly. It also takes a copy off the hold shelf that somebody was told to come for,
which is the one outcome a hold exists to prevent.

**An update rather than an event** — Catalog reaching into the other schemas, or a foreign key
carrying the change. Impossible by construction here and rejected long before this record: no
constraint crosses a schema, and a module reaching into another's tables ends the boundary that
every other decision rests on.

**One contract for both merges.** `RecordsMerged(absorbedId, survivingId)` would serve editions and
members alike, and it is exactly the false economy the published languages exist to prevent: the
two are published by different modules, consumed by different subscribers, and mean different
things. A shared contract would make Members and Catalog change together forever.

**Carrying more than the two identifiers** — the absorbed record's fields, so a consumer could log
or display them. No consumer needs them, and the ones that would find a use are the ones that
should be asking Catalog instead.
