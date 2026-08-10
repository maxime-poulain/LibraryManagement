# Tactical design — Catalog

The bibliographic description of what the library holds, independent of any library: what a thing
*is*, before anyone owns a copy of it or lends one. The most upstream context in the map — every
lifecycle in the system begins with a record here — and, until this document, the only one whose
decisions lived in its clients' margins rather than in a page of its own. The rules below were
decided when the module was built; this document gives them the home the design-first rule always
owed them.

Boundaries come from [strategic-design.md](strategic-design.md). This document decides aggregates,
invariants and the moments where they meet.

## 1. What the business can change

Nothing, and that is a finding about the subdomain rather than an omission. Circulation's numbers
are decisions of *this* library; a bibliographic description follows rules the profession settled
long ago, and a library does not become better by inventing its own. There is no policy object
because there is no lever: the length bounds on a title or a name form are conventions, exactly as
Holdings' two are, and the day a genuine local decision appears — which classification scheme to
prefer, say — it earns a place then.

## 2. The aggregates

Three roots, and the boundaries between them are the section worth reading twice, because every
one of them refuses a navigation somebody would expect.

```
Work
  WorkId          identity
  PreferredTitle  the title it is filed under — a cataloger's decision, not a description
  AuthorIds       who is credited, as identifiers — see below

Author
  AuthorId        identity
  PreferredName   the form the person is filed under, in filing order (NameForm)
  VariantNames    the forms that lead back to it
  LifeYears       birth and death, either unknown — a person's fact, Unknown for a body

Edition
  EditionId       identity
  WorkId          the work it prints
  Isbn?           when it bears one
```

**A work credits authors by identifier, and the author knows nothing of it.** `Work.AuthorIds` is
a set of identifiers, never a navigation: crediting an author requires the author already
registered — a rule spanning two aggregates that neither can hold, so the handler asks, the same
shape every cross-aggregate rule in this solution takes. "The works of this author" is a query.

**An edition is a root of its own, and thin on purpose.** Hold queues key on an edition, so an
edition must be independently addressable — the strategic design's open question, settled by the
hold model. It holds an identity, its work, and the ISBN it bears when it bears one; no ISBN is
not an oversight, since grey literature and everything printed before 1970 carry none, and those
are exactly what the manual commands exist to catalog. The publisher, the format, a translation's
contributors arrive with their own concepts the day they are modeled — a thin edition is what let
Holdings attach copies without waiting for them. A work knows nothing of its editions: "the
editions of this work" is a query, not a navigation.

**Registering an edition requires the work already cataloged** — the author-credit rule again,
enforced the same way, refused with a sentence a foreign key could never say.

## 3. Names, and the two ways one changes

The pair this context teaches the rest of the solution.

**`Rename` records a fact about the person.** A pen name adopted, a marriage: the old preferred
name is demoted to a variant, because people keep looking for the form they knew — it stays a
searchable access point that leads to the record.

**`CorrectPreferredName` records a fact about the record.** A typo is nobody's name: kept as a
variant it would become a searchable access point, and the catalog would preserve forever the one
thing it was asked to remove. Nothing is kept.

The two operations differ in exactly what they leave behind, and that difference is why they are
two. Members §7 records the counterpart decision: a member has no variant forms, so one `Rename`
carries the marriage and the typo alike — the distinction earns its keep only where access points
exist.

A variant equal to the preferred name is refused; renaming to an existing variant promotes it and
demotes the old preferred form: the set of forms stays consistent because one aggregate owns all
of them.

## 4. The title

`PreferredTitle`, never plain `Title` in prose or glossary: the day an edition carries the title
printed on its own title page (`TitleProper`, reserved in the strategic design), the word would
otherwise mean two things in one context.

**A retitled work keeps no memory of its former title.** The model records no title variants — so
`WorkRetitled` is a replacement, not a demotion, and it carries both titles because the search
projection must retract one access point and add the other. The day former titles must stay
findable, the model gains title variants first and this event follows; that order is recorded here
so it happens as a decision.

## 5. Search is the catalog's own index

`AccessPoint` — the profession's word, kept: every preferred name, variant form and title is one,
and the question it answers is *which records answer to this form?* A read-model row, anemic like
`OutboxMessage`, fed by this module's events through the outbox, rebuildable from them; the truth
lives in the aggregates. This is the Catalog-local slice of the strategic design's §7: search is a
projection, never a context — and `Discovery`, the cross-module list with copies and availability,
keeps its reserved name for the day it is built.

## 6. The one question others ask

The context map makes Catalog the supplier of one fact: **does this edition exist, and what is it
called?** `IEditionCatalog` answers with existence and a summary — an identifier plus enough words
for another context to name an edition without reproducing its description. Holdings asks at
accession; nothing else crosses. An Open Host Service in the proper sense, like Holdings'
lendability: published by the upstream, for anyone.

## 7. The moments

Manual commands, and deliberately the fallback rather than the main flow — the real feed is the
import §9 defers. Registering a work (optionally crediting authors as it is cataloged), crediting
and removing an author's credit, retitling; registering an author, renaming, correcting the
preferred name, adding variants, correcting life years; registering an edition. Each follows the
solution's shape — refusals with named codes, cross-aggregate questions asked by handlers — and
none needs a clock: bibliography has no deadlines.

**Merging two editions** joins them, and it is the first moment here that is not simply a record
being written. One record absorbs the other and the absorbed one becomes a pointer: `AbsorbedInto`
is terminal the way a member's erasure is, and for the same reason — three contexts hold this
identifier, so the row stays and stops being a record rather than being deleted.

**Its four rules live together, in `EditionMergeDomainService`** — the first domain service in the
solution. A record cannot absorb itself; a record already absorbed no longer acts; the survivor must
itself be live; and both must print the same work. That last one is the guard against the one
mistake nothing recovers: merging editions of different books tells Holdings to repoint copies onto
another book, and nothing recorded which ones moved. `Edition.AbsorbInto` is `internal` behind the
service and does only what remains once the decision is made — the state change and the event — so
the rules cannot be reached around. What stays in the handler is the one question that needs the
store: *do these identifiers resolve?*

Merging two *authority records* is the other half and is not built — it stays inside this context,
since no module outside it holds an `AuthorId`.

## 8. Events

Facts about records, consumed today by the access-point projection and published all the same, for
the standing reason: an event not published when it happened cannot be recovered afterwards.

| Event | Consumed by |
|---|---|
| `WorkRegistered`, `WorkRetitled(…, previousTitle, newTitle)` | access points |
| `WorkAuthorCredited`, `WorkAuthorCreditRemoved` | nothing yet |
| `AuthorRegistered`, `AuthorRenamed(…, previous, new)`, `AuthorPreferredNameCorrected(…, previous, corrected)`, `AuthorVariantNameAdded` | access points |
| `AuthorLifeYearsCorrected(…, previous, corrected)` | nothing yet |
| `EditionRegistered(…, workId, isbn?)` | read model |
| `EditionsMerged(absorbedEditionId, survivingEditionId)` | access points, **and the modules downstream** |

The corrected/renamed pairs carry both forms because a projection keyed on the old one must
retract it — the shape `CopyRelabelled` and `CardReplaced` later reused, and the shape
`AuthorLifeYearsCorrected` follows even with no projection to feed: what a corrected pair owes its
consumers does not depend on how many it has.

**Three of these existed only as a gap until this document surfaced it.** Crediting an author,
removing a credit, and correcting life years changed a record and raised no event — they predate
the access-point index, which none of them feeds, and nothing else listened, so nothing forced the
question. The standing rule says an event not published when it happened cannot be recovered
afterwards, and the stock ledger and member file both earned their streams on that argument; the
first version of this section recorded the three as the one place the context fell short of the
solution's own bar, and the three events above are the fix it predicted — three events, not a
redesign. They are published for the standing reason alone: `nothing yet` in the table is a
statement about today's consumers, never part of the contract.

Two rules the fix enforced are worth their sentences. A removal that removed nothing announces
nothing — the command reads "reach this state" and treats the already-reached state as success, so
the aggregate is the only place that still knows whether anything happened, and an event may only
say it did. And a registration is not a correction: an author registered with known years, or a
work registered with its authors, announces one opening fact first — `Register` raises its own
event before the credits it records, and sets the years without passing through the correction —
so the stream never credits a work that does not yet exist and never claims a repair of a reading
that never stood.

**Consumed: nothing.** Only the external supplier is upstream of Catalog, and its records arrive
through an anticorruption layer, not a subscription.

## 9. Deliberately left out

**The MARC import.** The real feed, and the reason this subdomain is supporting: a record for a
given ISBN is the same everywhere, so records come from a bibliographic supplier in a MARC-family
format, through an anticorruption layer that lets nothing shaped like MARC past the border. Named
in the strategic design so nobody mistakes the manual fallback for the design; deferred because an
import pipeline is shaped by operational concerns — batching, matching, review — that need a host.

**Most of the glossary's nouns.** `Publisher`, `Series`, `Subject`, `ClassificationScheme`,
`ClassNumber`, `MaterialType` are glossary rows without types: named so their arrival is a
decision with its vocabulary ready, unbuilt because nothing downstream needs them yet — Holdings
builds a shelfmark from a class number a cataloger reads off another system today, and Circulation
indexes no rule by material type while the policy is flat. Each is an addition; none is a rewrite.

**Contributors beyond the author.** A translator or illustrator is an edition-level fact in a
three-level model, and the glossary already reserves the renames it will force — `Author` to
`Agent`, `AuthorIds` to contributions with roles, `LifeYears` to `ExistenceDates`. To settle when
the edition grows its facts, alongside the publisher and the format.

## 10. Consequences and open questions

**What building it taught, recorded where the lessons struck.** The owned-collection key defect —
an author's credit silently dropped because the convention read its key as store-generated — was
found here first and is now a model rule in every module (`ValueGeneratedNever`, Circulation §10
tells the full story). The access-point kind stored as its name, not its number, became the
solution-wide rule that statuses humans read are stored as strings. And the Rename/Correct pair
became the reference other contexts decide against, twice: Members declining it, Holdings leaning
on it for the mis-cataloged copy. Closing §8's gap taught one more: a factory that reuses its own
mutators raises their events, so `Register` must announce the opening before the facts it records
in passing — an ordering the outbox preserves and no consumer should have to repair.

Open, each deferred for a stated reason rather than forgotten:

* **Merging two records was the gap every client had named. This context's half of it is now
  built**, under [ADR-0017](adr/0017-a-merge-is-an-event-and-circulation-pays-for-it.md), which
  decides the whole shape: `EditionsMerged` crosses, carrying the absorbed identifier and the
  surviving one and nothing else, and each consumer dispatches a command of its own. **The first
  consumer is now listening**: Holdings refiles every copy of the absorbed record under the
  survivor, which is what turned this context's announcement from a fact stated into a fact acted
  on. Circulation and Members remain, and the record names the order.
  Two things the record establishes are worth repeating here. The **authority** merge does not
  cross at all, since no module outside this context holds an `AuthorId` — it is an internal change
  to `Work.AuthorIds` and the access-point index, and it is not built either. And the expensive
  consumer is **Circulation**, not the two this document used to name: `HoldQueue` is keyed by
  `EditionId`, so a merge there makes two aggregates into one. The import (§9) is what will force
  the whole thing, since matching incoming records against existing ones is where duplicates
  surface.
* **Where a rule spanning two aggregates goes, settled here.** The first version of the merge split
  its four rules between the aggregate and the command handler, and cited the precedent this
  context already had: *crediting an author who does not exist spans two aggregates, so the handler
  asks the question*. The citation was wrong, and seeing why produced the rule the solution now
  follows. That precedent turns on a **lookup** — no aggregate can answer *does this identifier
  exist?* without a store. A merge's remaining rules need no store at all once both records are
  loaded; they are comparisons. So the boundary is not *how many aggregates* but **whether the store
  must be asked**: a lookup is the handler's, a rule over aggregates in hand belongs to the domain,
  and when it belongs to none of them alone it becomes a **domain service**.
  `EditionMergeDomainService` is the first, `Edition.AbsorbInto` is `internal` behind it, and
  `RegisterWork`/`RegisterEdition` stay handlers — correctly, since neither ever holds two
  aggregates. CLAUDE.md carries the rule with that counter-example attached, because a rule read
  without it turns two good handlers into two empty services.
* **What building this half taught.** Two things the paper design had not reached. The forms of an
  absorbed record **move** to the survivor rather than being deleted — an ISBN somebody used to
  find the old record is still printed on the book in their hand, and deleting it would make a
  correction of the catalog look, to the person searching, exactly like the record having been
  lost. They arrive as variants and never as preferred, which is the distinction §2 already draws
  between a preferred name and a variant, applied to what a merge produces. And
  `IEditionCatalog.ExistsAsync` had to start answering **false** for an absorbed record: Holdings
  asks it before attaching a copy, and attaching one to a record just merged away would
  manufacture, one acquisition at a time, the very orphan the event exists to repair.
* **Whether a hold may be placed on a work** — any edition will do — as well as on an edition.
  Members ask for both; the queue rules differ; the strategic design keeps the question.
* **`EditionStatement`**, ISBD area 2, when the edition thickens (§9): a fact carried by an
  edition that the profession's word for is the edition's own name, which is exactly why the
  glossary row exists already.
