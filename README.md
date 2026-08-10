# LibraryManagement

A library management system for the staff of a single public library, built as a **modular monolith**
on .NET 10 — five DDD bounded contexts over a small technical shared kernel.

It is a showcase of architecture rather than of features. The goal is a codebase where every
structural decision is deliberate, argued somewhere, and enforced by something. Where a choice was
made, the document says what was rejected and why; where a decision is deferred, it names the trigger
that reopens it.

**It is design-first**: every context is decided on paper in [`docs/`](docs/) before it is coded, and
those documents are the authority. When the code and a document disagree, one of them is a bug and
the pair is fixed in the same change.

---

## State

All five bounded contexts are implemented and tested — Catalog, Holdings, Circulation, Members and
Charges — along with the passage that carries a fact from one module to another.

The one cycle the context map draws now turns both ways: Circulation announces a return or a loss,
Charges prices it, Charges announces the amount, and Circulation judges it against its own threshold
and cancels the borrower's holds.

It **runs**: `src/Host/LibraryManagement.Host` composes the five modules, applies their migrations at
startup, and puts the five outbox drains, the daily process and the outbox purge on a clock
([ADR-0015](docs/adr/0015-the-first-host.md)).

And it **reads**: the member's file — who the person is, what they have out, what they owe — is
composed at the edge from three modules' published queries, which is the one arrangement the
strategic design had decided in full and nothing had yet exercised.

Catalog has begun the merge [ADR-0017](docs/adr/0017-a-merge-is-an-event-and-circulation-pays-for-it.md)
decided, and it is built for editions: two records join, the absorbed one becomes a pointer, and
`EditionsMerged` is announced. **Two modules listen** — Holdings refiles every copy of the absorbed
record under the survivor, and Circulation both points every loan *still out* at it and makes the two
hold queues into one, while an ended loan keeps saying what was borrowed. The queue merge is where it
was expensive: an invariant had to be restated, and the desk act *cancel my hold* had to start naming
which hold rather than guessing between two. What is left is the same question asked of **members**.

**Deliberately absent, each for a recorded reason:**

| Missing | Why | Record |
|---|---|---|
| Notifications, Staff access | Generic subdomains, out of the modeled domain | [strategic design §6](docs/strategic-design.md) |
| MARC import | The Catalog's real feed; the manual commands are the fallback, not the design | [strategic design §6](docs/strategic-design.md) |
| Merging two members | The other half of ADR-0017, reaching the hold queues from the other side: two borrowers' claims combine in the same queues, against the same invariant the edition merge restated. It reuses that machinery rather than needing its own | [ADR-0017](docs/adr/0017-a-merge-is-an-event-and-circulation-pays-for-it.md) |
| Merging two authority records | The other half of a merge, and the easy one: no module outside Catalog holds an `AuthorId`, so it never crosses a boundary | [ADR-0017](docs/adr/0017-a-merge-is-an-event-and-circulation-pays-for-it.md) |

---

## The domain in four questions

Everything the system does exists to answer one of these:

- **What is this?** — the bibliographic description of a book, independent of any library → **Catalog**
- **What do we own, and where is it?** — this library's stock, its condition, its shelf → **Holdings**
- **Who has it, until when, and who is waiting?** → **Circulation** *(core)*
- **Who owes us what?** → **Charges**

The third is the one a library is *for*. The first two exist to make it answerable, and the fourth is
a consequence of it. **Members** answers who is entitled to borrow at all.

### The boundary test

One question decided most of the context map:

> **Can these two facts disagree for a few seconds without a librarian noticing something is wrong?**
>
> If **no**, they belong to the same context — the rule tying them is an invariant, and an invariant
> spanning two contexts is not enforced, only hoped for.
> If **yes**, they belong to different contexts — the rule is a convergence, and convergence is what
> messages between contexts are for.

Applied in both directions: **holds belong with loans** (a copy shelved that was promised is visible
at the desk, to the member), **fines do not** (a fine created two hundred milliseconds after a late
return is invisible to everyone).

### Context map

```mermaid
flowchart TD
    SUP["Bibliographic supplier<br/><i>external</i>"]
    CAT["<b>Catalog</b><br/><i>supporting</i>"]
    HLD["<b>Holdings</b><br/><i>supporting</i>"]
    MEM["<b>Members</b><br/><i>supporting</i>"]
    CIR["<b>Circulation</b><br/><i>core</i>"]
    CHG["<b>Charges</b><br/><i>supporting</i>"]

    SUP -->|"Conformist + ACL<br/>MARC records in"| CAT
    CAT -->|"Published Language<br/>EditionId"| HLD
    CAT -->|"event<br/>two records became one"| HLD
    CAT -->|"event<br/>two records became one"| CIR
    HLD -->|"Customer / Supplier<br/>may this copy be lent?"| CIR
    MEM -->|"Customer / Supplier + ACL<br/>Member → Borrower"| CIR
    CIR -->|"events<br/>returned, given up on"| CHG
    CIR -->|"events<br/>copy lost, copy came back"| HLD
    HLD -->|"events<br/>copy left service, copy turned up"| CIR
    HLD -->|"event<br/>copy turned up"| CHG
    CHG -->|"event<br/>this member's balance moved"| CIR
    CHG -.->|"how much does this<br/>member owe?"| CIR

    style CIR stroke-width:3px
```

Solid arrows point downstream — at the context that must adapt when the other changes. Dashed arrows
are queries: they carry no authority and change nothing.

**An arrow out of the core announces and never instructs.** Circulation reports that a copy is
unaccounted for; that this makes it `Lost` is Holdings' rule, reached identically by a stocktake that
failed to find it. Nothing depends on *how Circulation decides* — the cap, the standing, the pickup
period — and those are the rules most likely to change.

The full map, including Notifications and the read model, is in
[`docs/strategic-design.md`](docs/strategic-design.md) §8.

---

## Architecture

| Concern | Approach |
|---|---|
| Deployment | Modular monolith — one process, one database, five modules ([ADR-0001](docs/adr/0001-modular-monolith-over-services.md)) |
| Layering | Clean Architecture — Domain / Application / Infrastructure per module |
| Modeling | DDD strategic + tactical — rich aggregates, value objects, domain events |
| Messaging in | CQRS over a source-generated mediator ([ADR-0005](docs/adr/0005-mediator-source-generator.md)) |
| Messaging out | Transactional outbox, one table per module ([ADR-0006](docs/adr/0006-transactional-outbox-per-module.md)) |
| Between modules | Published languages of primitives only ([ADR-0007](docs/adr/0007-published-language-only.md)) |
| Failure | `Result` for refusals, exceptions for faults ([ADR-0004](docs/adr/0004-result-over-exceptions.md)) |
| Persistence | Repository + unit of work; optimistic concurrency via `rowversion` |
| Enforcement | Executable architecture tests ([ADR-0011](docs/adr/0011-architecture-rules-are-tests.md)) |

### Module anatomy

Each bounded context is four projects:

```
LibraryManagement.<Module>.Domain             aggregates, value objects, domain events, repository
                                              interfaces, error codes
LibraryManagement.<Module>.Application        commands, handlers, validators, queries, DTOs
LibraryManagement.<Module>.Infrastructure     DbContext, EF configurations, repositories, query
                                              handlers, event translators, subscribers, DI
LibraryManagement.<Module>.PublishedLanguage  what this module says to the ones downstream of it —
                                              references nothing, primitives only
```

A module owns its `DbContext`, its schema, its domain model, its application layer and its
infrastructure. **No module references another's Domain, Application or Infrastructure** — only its
`PublishedLanguage`.

### Domain events and integration contracts are different things

They are never mixed, and the distinction is structural rather than stylistic.

**A domain event** stays inside its module. It carries the module's own types — `LoanId`, `CopyId`,
value objects that validate on creation — and is written to that module's outbox table in the *same
save* as the change that raised it. A scheduler drains the table and delivers each event later, in a
transaction of its own.

**An integration contract** crosses a boundary. It is a flat `record` of primitives in the publishing
module's `PublishedLanguage`, which references nothing at all — not even the shared kernel, so it
cannot implement a marker interface, so the mediator cannot carry this hop. Subscribers are resolved
from the container by the contract's own type.

One domain event is flattened into **as many contracts as it has audiences**:

```
LoanDeclaredLost  ──translator──▶  CopyReportedLost (copy)              ──▶  Holdings
                  └─translator──▶  LoanEndedUnreturned (copy + borrower) ──▶  Charges
```

One with a single audience still gets its own contract, for the same reason:

```
EditionsMerged    ──translator──▶  EditionsMerged (two edition ids)     ──▶  Holdings
```

**And a subscriber does not write — it dispatches a command of its own module**
([ADR-0008](docs/adr/0008-subscriber-dispatches-its-own-command.md)). The drain's save is on the
*publisher's* context, so a subscriber touching its own aggregates would leave them tracked in a
context nobody saves — persisted nowhere, reported by nothing.

---

## Build, test and run

Requires the **.NET 10 SDK**. Integration tests additionally need **Docker**; running the host needs
a SQL Server it can reach.

```bash
dotnet build LibraryManagement.slnx --configuration Release

# Full suite — needs Docker
dotnet test LibraryManagement.slnx --configuration Release --no-build

# CI's required check — no Docker needed
dotnet test LibraryManagement.slnx --configuration Release --no-build \
            --filter "Category!=Integration"

# The host: composes the five modules, migrates them, serves, and runs the schedule
dotnet run --project src/Host/LibraryManagement.Host
```

The host reads `ConnectionStrings:LibraryManagement` and refuses to start without it — which
database serves the modules is its decision, and it will not invent one. It applies every module's
migrations at startup, then puts the five outbox drains on `Cron.Minutely` and the daily process and
the outbox purge on `Cron.Daily`; `/hangfire` shows them, to local requests only. `Hangfire:RunServer`
turns the scheduled half off for a process that should only serve. `X-Employee-Id` on a request is
what the audit columns record — attribution, not authentication, as
[ADR-0015](docs/adr/0015-the-first-host.md) says.

### The desk's API

One route group per module, minimal APIs, and **the command record is the request body** — there is
no request type in between, because a command is already a flat record of primitives that a
validator refuses when it is wrong.

```http
POST /catalog/authors          { "authorId": "...", "preferredName": "Ernaux, Annie", "birthYear": 1940 }
POST /circulation/loans        { "loanId": "...", "copyId": "...", "borrowerId": "..." }
POST /circulation/returns      { "copyId": "...", "returnedDamaged": false }
POST /members/erase            { "memberId": "..." }
GET  /catalog/works/{workId}
GET  /catalog/search?formPrefix=Ern
GET  /members/{memberId}/file
```

**Only what a librarian does is routed** — 37 of the 52 commands. Seven belong to the daily process,
and eight exist because one module reacts to another; a route for those would let a request forge a
fact only the announcing module is entitled to state.

A command answers **204**, a query **200**. A refusal is **422** — the request was understood and
the domain said no — with **400** for validation and **409** for a concurrency conflict; only a query
answers **404**, because a command's route names an act that exists whether or not its referent
does. Every refusal carries a problem document listing *all* its errors with the module's own codes.

### The one page that is composed rather than routed

`GET /members/{memberId}/file` belongs to no module, and that is the point. Members publishes who
the person is, Circulation the loans, the holds and the `Standing`, Charges the balance; the host
dispatches the three and puts the answers side by side. It **assembles and decides nothing** —
whether a member may still borrow is judged by Circulation and displayed as given, because a rule
that slips into a composer is a rule no module's invariants cover.

Each module answers with a `Result`, so the page chooses its own degradation: it returns the parts
it has and **names the parts it does not**, since a blank balance panel is indistinguishable from a
member who owes nothing. Only Members' refusal empties the page — a file is a file *of* somebody —
and that one travels as the 404 it is. [ADR-0016](docs/adr/0016-a-composed-page-degrades-in-parts.md)
records the decision and what it rejected;
[strategic design §10](docs/strategic-design.md) decided the arrangement long before it was built.

The unit filter runs **1184 tests across 20 projects**.

Integration tests start SQL Server 2022 through Testcontainers, or target the server named by the
`LIBRARYMANAGEMENT_TEST_SQLSERVER` environment variable. Each drops its database and applies that
module's migrations, so the suite exercises the schema a deployment would get; adding one is
[`migrations.md`](docs/migrations.md) §3, and `dotnet tool restore` puts `dotnet ef` in place.

**The unit/integration split is by trait, not by project** — two test projects are mixed. Integration
classes carry `[Collection(SqlServerCollection.Name)]` and `[Trait("Category", "Integration")]` as a
pair. An unmarked test runs everywhere, so forgetting the trait costs a slow build rather than a
silent hole in the gate.

Test projects are executables: xUnit v3 on Microsoft.Testing.Platform, asserting with Shouldly.
Package versions live in `Directory.Packages.props` (central package management); `NuGet.config`
clears inherited sources on purpose.

### Continuous integration

`.github/workflows/ci.yml` runs on every pull request. The required status check is the **job name**,
`Build and unit tests` — renaming it strands the branch ruleset on a context nobody produces.

Pull requests from `claude/**` branches additionally get a dedicated `Integration tests` check.
`.github/workflows/sonarcloud.yml` runs the whole suite with coverage and is not required for
merging.

---

## Layout

```
src/Shared/       Technical kernel: Entity, ValueObject, Result, CQS, pipeline behaviors,
                  outbox, audit. Building blocks only — never business concepts.
src/<Module>/     One bounded context, four projects (above).
src/Host/         The runnable host, and one migrations project per module beside it. The only
                  place that names the database engine or the scheduler.
tests/            Mirrors src/, plus Architecture.Tests (reflection rules over the built
                  assemblies), Composition.Tests (whole pipeline, all modules) and Host.Tests
                  (the real host, booted).
docs/             The design. Authoritative.
docs/adr/         Architecture decision records.
```

**There is no shared kernel in the DDD sense.** `Shared.Domain` and `Shared.Application` hold
`Entity`, `ValueObject`, `Result` and the CQS abstractions — building blocks, not business concepts.
No context shares a domain model with another, and none should: a shared `Book` between Catalog and
Circulation would be the first step back to a single model with five namespaces.

---

## Rules the code holds to

Most are enforced — by the compiler, the container, an index, or an architecture test. **A change that
violates one should fail somewhere; if it does not, that missing enforcement is the first bug to fix.**

**Boundaries.** One `DbContext`, one schema per module, and **no foreign key ever crosses a schema** —
a cross-module reference is an identifier, redeclared locally. **No module names a database provider
or a scheduler**; both are host decisions, and the provider lives in `src/Host/` beside the
migrations that cannot avoid naming it.

**Write path.** A command is the unit of consistency. Handlers and repositories never call
`SaveChangesAsync` — `UnitOfWorkBehavior` writes once, on success, through the unit of work of the
module the command belongs to. A command handler never depends on a dispatcher. **No invariant rides
on an event handler**: anything that must hold in the same instant as the command is written in the
handler, on the aggregates, directly.

**Contracts.** A query answers with a `Dto`-suffixed type. An aggregate maps through
`AggregateRootConfiguration<,>`, which carries the key conversion and the `rowversion` token whose
omission fails silently. **Every key property of an `OwnsMany` collection says `ValueGeneratedNever()`**
— the convention reads a `Guid` key as store-generated, so an entry added to an aggregate the store
already holds arrives with its key filled in and EF writes an UPDATE that matches nothing, or no
statement at all. Statuses humans read in a table are stored as strings.

**Language.** The glossary in [`docs/strategic-design.md`](docs/strategic-design.md) §4 is binding.
American spelling throughout — `Catalog`, never `Catalogue`. Its most-violated edges: `PreferredName`
and `VariantName` (never *Heading*), `Copy` (never *Item*), `Shelfmark` (never *call number*),
`InService` (never *OnShelf*), `Balance` in Charges but `Debt` and `Standing` in Circulation.

**Prose.** XML docs and comments state the constraint and the alternative that was rejected — never
what the next line does. Commit subjects are plain sentences, not conventional-commit prefixes; the
body carries the narrative, and it should read as one.

**History.** A branch under review carries exactly one commit. Every further push re-squashes the
whole branch onto its base and force-pushes with `--force-with-lease`, so what a reviewer reads is
the change rather than the steps that reached it. The pull request is opened with the first push of
a branch and updated from then on — never a second one for the same work. Neither a commit message
nor a pull request body carries a session URL — a link nobody outside the conversation can open
dates the moment instead of explaining the change. The `Co-authored-by` trailer stays; it is a fact
about authorship.

---

## Documentation

The design documents are the source of truth. Read the relevant one before modeling anything.

| Document | Decides |
|---|---|
| [`strategic-design.md`](docs/strategic-design.md) | Boundaries, subdomains, context map. §4 is the **binding glossary**; §10 is the codebase rules. |
| [`tactical-design-catalog.md`](docs/tactical-design-catalog.md) | Catalog's aggregates, the two ways a name changes, and the merge question every client has named. |
| [`tactical-design-circulation.md`](docs/tactical-design-circulation.md) | Circulation's aggregates, invariants, desk moments and daily process. |
| [`tactical-design-holdings.md`](docs/tactical-design-holdings.md) | Holdings' aggregate and moments, including what it owes a merge in Catalog. |
| [`tactical-design-members.md`](docs/tactical-design-members.md) | Members' aggregate and moments. |
| [`tactical-design-charges.md`](docs/tactical-design-charges.md) | Charges' aggregate, invariants and moments. |
| [`outbox.md`](docs/outbox.md) | Domain events: same-save storage, drain, failure semantics, the cross-module passage (§9). |
| [`migrations.md`](docs/migrations.md) | One migrations project per module, a history table per schema, and why it took until the first host. |
| [`adr/`](docs/adr/README.md) | Seventeen decision records — what was decided, when, and what it costs. |

Each tactical design's **§10 records what building the module taught** — including the places the
code corrected the design, which are usually the most useful paragraphs in the document.

---

## Contributing

- Respect module boundaries and preserve the ubiquitous language.
- Keep the domain model rich; prefer explicit code over clever abstractions.
- **Challenge architectural decisions rather than implementing them blindly** — the documents record
  rejected alternatives so that revisiting one is a decision rather than an accident.
- Keep the documents and the code synchronized. They are one artifact.

**Verification bar for any change:** a Release build with zero new warnings; the full suite green with
Docker up; the `Category!=Integration` filter green with Docker stopped — that is exactly CI's view;
and if a glossary term moved, a lexical sweep proving the abandoned form is gone everywhere except
lines that name it as abandoned.

## License

[MIT](LICENSE).
