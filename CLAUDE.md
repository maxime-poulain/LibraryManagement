# CLAUDE.md

Session context for AI-assisted work in this repository. The documents under `docs/` are the
authority on the domain and the architecture; this file is the map to them, plus what a fresh
session needs to build, test and contribute without rediscovering the rules the hard way.

## What this is

A library management system for the staff of a single public library: a modular monolith on
.NET 10, organized as five DDD bounded contexts — Catalog, Holdings, Circulation (core), Members,
Charges — over a small technical shared kernel. The project is design-first: every context is
decided on paper in `docs/` before it is coded, and the documents record rejected alternatives,
not just outcomes.

**State.** All five bounded contexts are implemented and tested. Circulation's desk moments,
its daily scheduled process, and Charges — the last one built — are in place, and so is the passage
that carries a fact from one module to another. The one cycle the context map draws now turns both
ways: Circulation announces a return or a loss, Charges prices it, Charges announces the amount, and
Circulation judges it against its own threshold and cancels the borrower's holds. Every module's
schema comes from its own migrations, in a project beside the host (`docs/migrations.md`), and the
host itself exists: `src/Host/LibraryManagement.Host` composes the five modules, migrates at startup
and owns the schedule (ADR-0015). The read side has begun: Members, Circulation and Charges each
publish a query, and the host composes the member's file at the edge from the three — the first
page, and the decision about what it does when a module cannot answer is ADR-0016. Catalog has
begun the merge ADR-0017 decides: two editions join, the absorbed record becomes a pointer, and
`EditionsMerged` is announced — and **two modules listen**: Holdings refiles every copy of the
absorbed record under the survivor, and Circulation both points every loan still out at it and makes
the two hold queues into one, leaving ended loans saying what was borrowed. **Members announces the
same fact about people** — `MembersMerged`, with the absorbed record kept intact behind a pointer
and the entitlement port answering *unknown* for it — **and Circulation consumes it**: the loans the
absorbed record has not yet answered for are repointed (a wider cut than the edition merge's
live-only sweep — a written-off loan still speaks its borrower the day its copy resurfaces), and the
person's claims are combined in every queue under the survival rules the queue merge wrote. What
remains is Charges' share of a merged member: the surviving account absorbing the other's
outstanding charges.

## The documents are the authority

Read the relevant one before modeling anything. When code and a document disagree, one of them
is a bug: fix the pair in the same change.

| Document | Decides |
|---|---|
| `docs/strategic-design.md` | Boundaries, subdomains, context map. §4 is the **glossary — the binding ubiquitous language**. §10 is the codebase rules. |
| `docs/tactical-design-catalog.md` | Catalog's aggregates and moments: the two ways a name changes, the thin edition, the access-point index, and the merge its clients had named — decided in ADR-0017, and this context's half of it built. |
| `docs/tactical-design-circulation.md` | Circulation's aggregates, invariants and moments. Desk moments, the daily process, both directions of the Charges cycle and both halves of an edition merge implemented; §10 records what building each taught, including why an invariant can be right and overstated at once. |
| `docs/tactical-design-holdings.md` | Holdings' aggregate and moments, including what it owes a merge in Catalog. Implemented; §10 records what building it taught, and which three guards it taught not to write. |
| `docs/tactical-design-members.md` | Members' aggregate and moments, including the merge of two records for one person. Implemented; §10 records what building it taught, including why two terminal states are not interchangeable. |
| `docs/tactical-design-charges.md` | Charges' aggregate, invariants and moments. Implemented; §10 records what building it taught, including the one place the mapping had to depart from the rest of the solution. |
| `docs/outbox.md` | Domain events: same-save storage, drain, failure semantics, the cross-module passage (§9), and what renames break. |
| `docs/migrations.md` | One migrations project per module, the history table per schema that makes five contexts share one database, and how to add a migration. |
| `docs/adr/` | Seventeen decision records — what was decided, when, what it costs, what was rejected. Navigation, not argument: where a decision is argued at length above, the record points there rather than restating it. Start at `docs/adr/README.md`. |
| `README.md` | The public face: state, context map, build and test, the rules, and the index to all of the above. It summarizes and never decides — when it disagrees with a document here, the document wins. |

## Build, test and run

```bash
dotnet build LibraryManagement.slnx --configuration Release
dotnet test  LibraryManagement.slnx --configuration Release --no-build   # full suite — needs Docker
dotnet test  LibraryManagement.slnx --configuration Release --no-build \
             --filter "Category!=Integration"                            # CI's view — no Docker
dotnet run   --project src/Host/LibraryManagement.Host                   # the host
```

- The host needs `ConnectionStrings:LibraryManagement` and refuses to start without it. It migrates
  all five modules at startup, then schedules the drains (`Cron.Minutely`), the daily run and the
  purge (`Cron.Daily`); `/hangfire` is the dashboard, local requests only. `Hangfire:RunServer=false`
  serves without running the schedule.
- **The API**: one minimal-API group per module, the command record *is* the JSON body, and only
  desk acts are routed — never the seven the daily process owns, never the commands a module
  dispatches for another. 204 for a command, 200 for a query, 422 for a refusal, 400 validation,
  409 concurrency; **404 only from a query**, since a command's route names an act that exists
  whether or not its referent does (ADR-0015).
- **The one composed route**, `GET /members/{id}/file`, lives in `MemberFileEndpoints` and belongs
  to no module: it dispatches Members', Circulation's and Charges' own queries and arranges the
  answers. It never derives anything — `Standing` is Circulation's verdict, shown as given — and it
  returns the parts it has while naming the parts it could not read (ADR-0016).
- `dotnet ef` comes from `dotnet tool restore`; adding a migration is `docs/migrations.md` §3.

- Integration tests start SQL Server 2022 through Testcontainers, or target the server named by
  the `LIBRARYMANAGEMENT_TEST_SQLSERVER` environment variable (a CI service container, a local
  instance).
- Test projects are executables: xUnit v3 on Microsoft.Testing.Platform.
- Package versions live in `Directory.Packages.props` (central package management);
  `NuGet.config` clears inherited sources on purpose — nuget.org only.

Sandboxed or remote sessions, observed once and likely again: if the .NET SDK is absent,
`apt-get install dotnet-sdk-10.0` works where `dot.net` is blocked; if Docker Hub's CDN is
blocked, `export TESTCONTAINERS_RYUK_DISABLED=true` (Ryuk is only the container reaper) after
pulling `mcr.microsoft.com/mssql/server:2022-latest`.

**CI** is `.github/workflows/ci.yml`. The required status check is the **job name**,
`Build and unit tests` — renaming it strands the branch ruleset on a context nobody produces.
The unit/integration split is **by trait, not by project** (two test projects are mixed):
integration classes carry `[Collection(SqlServerCollection.Name)]` and
`[Trait("Category", "Integration")]` as a pair. An unmarked test runs everywhere — the failure
mode of forgetting is a slow build, never a silent hole in the gate.

A second workflow, `.github/workflows/sonarcloud.yml`, is not required for merging: it runs the
**whole** suite (hosted runners have Docker, so this is where the integration tests run
automatically) and sends coverage to SonarCloud. It needs the `SONAR_TOKEN` secret and skips on
forked pull requests. Pull requests from `claude/**` branches additionally get a dedicated
`Integration tests` check in `ci.yml` — same tests, but a signal that names the failure and does
not depend on the Sonar token.

## Layout

```
src/Shared/       Technical kernel: Entity, ValueObject, Result, CQS, pipeline behaviors,
                  outbox, audit. Building blocks only — never business concepts.
src/<Module>/     One bounded context: Domain / Application / Infrastructure /
                  PublishedLanguage (what the module says to the ones downstream of it).
src/Host/         The runnable host, plus LibraryManagement.<Module>.Migrations.SqlServer,
                  one per module — its migrations, its design-time factory, and the single
                  Use<Module>SqlServer that everyone configures through. The only place that
                  names the engine or the scheduler.
tests/            Mirrors src/, plus Architecture.Tests (reflection rules over the built
                  assemblies), Composition.Tests (whole pipeline, all five modules and their
                  five outbox drains) and Host.Tests (the real host, booted).
docs/             The design. Authoritative.
docs/adr/         Decision records — navigation over the above, never a second authority.
README.md         The public face. Summarizes; decides nothing.
```

## Rules the code holds to

Most are enforced — by the compiler, the container, an index, or an architecture test. A change
that violates one should fail somewhere; if it does not, that missing enforcement is the first
bug to fix.

**Boundaries.**
- One `DbContext`, one schema per module; **no foreign key ever crosses a schema**. A
  cross-module reference is an identifier, redeclared locally (Holdings has its own `EditionId`
  carrying the same `Guid` as Catalog's — the model is translated, never the identity).
- Modules talk only through `*.PublishedLanguage` projects, which reference nothing and traffic
  in primitives — never a module's own types. No module references another's Domain,
  Application or Infrastructure.
- No **module** names a database provider (EF `Relational` only) or a scheduler (no Hangfire).
  Both are host decisions, and both are named in `src/Host/` — the provider by the migrations
  projects, which cannot avoid naming one, and Hangfire by the host itself, whose whole scheduling
  surface is `OutboxJobs`, `CirculationDailyRun` and `OutboxPurgeJob`.

**Write path.**
- A command is the unit of consistency. Handlers and repositories never call
  `SaveChangesAsync`; `UnitOfWorkBehavior` writes once, on success, through the module's unit of
  work — registered per module with `AddModuleUnitOfWork`, keyed by the command's assembly.
- A command handler never depends on `ICommandDispatcher` or `IQueryDispatcher`
  (architecture rule: no nested dispatch, no reads through the query pipeline).
- No invariant rides on an event handler. Events become outbox rows in the module's own schema,
  in the same save as the change; handlers are idempotent by `EventId` and never save.
- **A fact crossing to another module is a flat contract, and the reacting module dispatches its own
  command rather than writing** (`docs/outbox.md` §9). The drain saves the *announcing* module's
  context, so a subscriber that touched its own aggregates would leave them in a context nobody
  saves. A translator on the announcing side flattens the domain event into a `*.PublishedLanguage`
  record of primitives; the subscriber turns it into a command, and `UnitOfWorkBehavior` saves the
  right store. An architecture rule refuses a subscriber that names anything but a published
  language.
- **Renaming a stored event type — or any positional parameter of an event `record` — breaks
  every stored payload** (`docs/outbox.md` §3). Free only while the outbox tables are empty.

**Contracts.**
- A query answers with a `Dto`-suffixed type (architecture rule).
- An aggregate maps through `AggregateRootConfiguration<,>` (architecture rule — it carries the
  key conversion and the `rowversion` concurrency token, whose omission fails silently).
- **Every key property of an `OwnsMany` collection says `ValueGeneratedNever()`** — the convention
  reads a `Guid` or `int` key as store-generated, and an entry added to an aggregate the store
  already holds then arrives with its key filled in, so EF marks it `Modified` and writes an UPDATE
  that matches nothing. Where the table is nothing but its key, it writes no statement at all and
  the addition disappears without an error. A model rule in each module's `*DbContextTests` holds
  it; a test that only ever adds before the *first* save will not.
- Statuses and enums that humans will read in a table are stored as strings.

**Language.** The glossary is binding, and these are its most-violated edges: `PreferredName`
and `VariantName` (never Heading or AuthorizedName), `PreferredTitle`, `NameForm`, `Copy` (never
Item), `Shelfmark` (never call number), `InService` (never OnShelf), `Balance` in Charges but
`Debt` and `Standing` in Circulation. American spelling throughout — `Catalog`, never
`Catalogue`. Error codes read `Module.PascalCase` and live in the module's `*ErrorCodes` class.

## Adding things

- **A use case**: one folder holding `{Command, CommandHandler, CommandValidator}` — copy an
  existing one. Validators are discovered per module; an unregistered validator silently never
  runs, which is why registration is part of `Add<Module>Module`.
- **A query**: `{Query, QueryValidator, *Dto}` in a folder of the module's **Application**, and the
  handler in its **Infrastructure** under `Queries/` — the read side has no domain to protect, so
  it has no application layer beyond the contract. Nothing to register: the validator comes with
  the assembly scan and the handler with the mediator's. Write the integration test at the same
  time — **a query's shape is not provable by the compiler**, and a projection that reads perfectly
  and does not translate is the ordinary failure here (Circulation §10 records one).
- **A module**: copy the Catalog/Holdings shape, then wire *all* of — `LibraryManagement.slnx`;
  a `ProjectReference` in Architecture.Tests **and** one pinned handler in
  `CommandHandlerRulesTests` (the scan only sees referenced assemblies, and green over nothing
  reads exactly like green over everything); `Add<Module>Module` in the composition tests plus a
  drain method in `OutboxJobs`; `MapOutbox()` in the module's `DbContext`.
- **Prose**: XML docs and comments state the constraint and the alternative that was rejected —
  never what the next line does. Commit subjects are plain sentences, not conventional-commit
  prefixes; the body carries the narrative, and it should read as one.
- **Commit trailers**: exactly one, `Co-authored-by: Claude <noreply@anthropic.com>`, and **never a
  session URL**. A `Claude-Session:` link points at a conversation nobody outside it can open, and
  it dates the moment rather than the change — the log is read years later by someone reconstructing
  why, and a dead link is worse than no link because it looks like it should work. What the commit
  has to justify itself with is its own message. The attribution stays because authorship is a fact
  about the change; the transcript is not.
- **Pull requests carry no session URL either**, for the same reason: a PR body outlives the
  conversation that produced it, and a link nobody can open decorates without informing. The
  attribution line may stay; the `https://claude.ai/code/session_…` link may not — strip it even
  when tooling appends one by default.
- **One commit per pull request.** A branch under review carries exactly one commit, and it stays
  one: every further push integrates the new work, **re-squashes the whole branch onto its base**,
  rewrites subject and body to cover everything the branch now does, keeps the single trailer, and
  force-pushes with `--force-with-lease` — never a bare `--force`, which would discard whatever
  reached the remote while you were working. The rejected alternative is the usual one: let the
  branch accumulate and squash at merge. It gives the same `main` and a worse review, because
  between the first push and the merge the *branch* is what a reviewer reads, and six commits
  invite review of the steps taken rather than of the change proposed. The pull request is the unit
  of review, so it is the unit of history too. The narrative is not lost — it moves from the log
  into the body, which is where someone reconstructing the decision years later already looks. The
  two rules above bind that rewritten message exactly as they bound the ones it replaces.
  **Pull Requests** below carries the workflow this implies — when the pull request is opened, and
  what to do on every push after the first.

## Repository philosophy

This repository is intentionally opinionated.

Architectural decisions are deliberate, documented, and should not be changed lightly.

If you disagree with an architectural decision, challenge it before changing it. Explain why another approach would be preferable, discuss the trade-offs, and only then implement the chosen solution.

Every accepted architectural decision should have an explicit rationale, preferably documented in the README, the Strategic Design, or an ADR. Whenever practical, important architectural decisions should also be enforced by automated architecture tests so that the codebase cannot silently drift away from its documented design.

## Development philosophy

Before implementing a solution, always understand the existing design.

Architecture consistency is more important than introducing new abstractions.

Prefer explicit code over clever code.

Preserve module boundaries, ubiquitous language and existing architectural decisions unless there is a compelling reason to change them.

When a simpler, more idiomatic or more maintainable design exists, explain it before implementing it rather than following a proposal blindly.

The objective of this repository is to demonstrate professional software architecture, not to maximize feature delivery.

## Strategic Design first

The Strategic Design documents describe the intended domain model.

Implementation should translate the Strategic Design into code rather than invent new concepts.

If the implementation reveals a weakness or inconsistency in the Strategic Design, explain it and propose an improvement before modifying the model.

## Architecture documentation

README, Strategic Design and ADRs are the source of truth.

Whenever an architectural decision changes:

- update the corresponding ADR or create a new one;
- update the Strategic Design if the domain model evolves;
- update the README when user-visible behaviour or architecture changes.

Code and documentation must always evolve together.

## Domain Services

A Domain Service represents domain logic that does not naturally belong to a single Aggregate.

### When to write one

**A business operation that spans several Aggregates belongs in a Domain Service** rather than in a
command handler — but the boundary is *not* the number of aggregates, and reading it that way would
make this rule false against code that is already correct. The boundary is **whether the store has
to be asked**:

- the question needs a **lookup** — *does this identifier resolve?* — → **the handler asks it**.
  That is orchestration, not domain logic, and it belongs where the unit of consistency is visible.
- the rule is about aggregates **already in hand** and needs no store, yet belongs to none of them
  alone → **a Domain Service**.

**The counter-example matters as much as the rule**, because it is the first thing a reader will
test it against: `RegisterWorkCommandHandler` and `RegisterEditionCommandHandler` each hold a rule
that spans two aggregates, in a handler, and both are right to. Neither ever has two aggregates —
it has one being created and an *identifier* whose referent may not exist — and a service could only
answer by taking a repository, which is the next rule. Applied without its boundary, this convention
would turn two good handlers into two empty services.

### The form

An abstraction and an implementation: `I<Name>DomainService` and a `sealed` class, **both in the
module's Domain**. Splitting a contract from its implementation across layers is what dependency
inversion is for, and there is nothing to invert here — a Domain Service names no infrastructure,
which is the same property that made it one. Registered in `Add<Module>Module` like every other
seam, and injected.

### Its dependencies

**A Domain Service may depend on a repository abstraction, but only when retrieving an Aggregate is
intrinsically part of its business logic. Otherwise the Aggregates it needs are loaded by the
Application layer and passed to it.**

The test for *intrinsically*, without which the word means "when convenient": **can the caller name
what to load?** If it can, loading is the caller's job. If the rule must consult a set the caller
cannot name in advance — a uniqueness sweep across a whole collection, choosing a candidate from a
queue — then retrieval is part of the rule and the repository belongs in the service.

Leaning toward the parameter keeps two things: the unit of consistency stays visible in the handler
— *a command is the unit of consistency* is the hardest write-path rule here — and the logic stays
decidable without I/O, so it tests as a function.

### The aggregate it guards

Where a Domain Service holds an aggregate's invariants, that aggregate's mutator becomes
**`internal`**, so the service is the only way in rather than one path among two. The module then
grants `InternalsVisibleTo` to its own test project, which is what lets the surface stay narrow
instead of widening to become testable.

### Naming

Every Domain Service must be explicitly suffixed with `DomainService`. Avoid the generic `*Service`
suffix for domain concepts.

**`EditionMergeDomainService` is the first in the repository and the reference example**: four rules
about two records, none of which needs a store once both are loaded, with `Edition.AbsorbInto`
internal behind it.

**`HoldQueueMergeDomainService` is the second, and it is worth reading next** because it answers the
question the first one leaves: the two aggregates are of the *same type*. Two `HoldQueue`s become
one, so no argument about which of them "owns" the rule can be made from the types — the rule is
about the pair, and that is the whole test. The `internal` mutator `AbsorbHoldsFrom` is the way in,
the handler holds only the questions the store must answer — do these queues exist, and does the
survivor need one opened — and the service holds what concerns the pair: whether they may be joined,
and that the union is read against the invariant afterwards.

**The member merge in Circulation is the convention's second counter-example, and the instructive
one**: the same collision — one borrower, two claims, one queue — reached from Members' merge
instead of Catalog's, and *no* service was written for it. The rule spans one aggregate, so it lives
in `HoldQueue.CombineClaimsOf`, public where the pair's mutators are internal. Writing it there
showed that one of the service's rules had always been the aggregate's own — *among a borrower's
queued claims, the earliest survives* is `HoldQueue.KeepEarliestQueuedClaimOf` now, invoked by the
service for the union and by `CombineClaimsOf` for the member merge. A convention that only ever
adds services is a convention nobody is testing; this is what applying it in the negative looks
like.

`MemberMergeDomainService` is the third and is not described here on purpose: it is the same shape
again, and a convention section that grows an entry per instance stops being a convention and
becomes a catalogue. Two examples that differ from each other are what a reader needs.

`Standing` in Circulation is the same kind of object — a stateless judgement — and predates this
convention: `public static`, in the Application layer, unsuffixed. Known, not yet reconciled.

## Commit messages

Commit messages follow the Linux kernel style.

Do not use Conventional Commit prefixes such as:

- `feat:`
- `fix:`
- `refactor:`
- `chore:`

A commit message must contain:

- a concise imperative subject;
- a blank line;
- an explanatory body describing the important changes and, when useful, the motivation behind them.

The commit history should read naturally without relying on prefixes.

## Design discussions

Do not assume the first proposed architecture is the best one.

When multiple designs are possible:

- compare them;
- explain their trade-offs;
- recommend the one that best fits this repository;
- implement it only after the design has been made explicit.

Architectural discussions are encouraged.

Blind implementation is not.

## Pull Requests

### The pull request is opened without being asked for

Work that needs a push needs a pull request, and the two are one act rather than two. **Do not ask
whether to open it.** On any change that reaches a working branch:

1. create the working branch if it does not exist, from the current target branch;
2. make the changes;
3. commit them — one commit, under the conventions below;
4. push the branch;
5. **open the pull request against the appropriate target branch, immediately and unprompted.**

Asking first buys nothing. The work is already pushed by then, the answer is always yes, and the
question costs a round trip at exactly the moment the branch is most likely to be forgotten. A
branch pushed without a pull request is invisible: it is not in anyone's review queue, no CI gate
reports on it, and the only record that it exists is a line in someone's terminal.

### One pull request per branch, and it is updated rather than replaced

The rule above fires **once per working branch**, when that branch is first pushed. It is not a
rule about pushes: a force-push, a correction, a review fix, a fifth revision of the same work all
go to the pull request that is already open.

**Before pushing, look for an existing pull request for the current branch.** If one is open,
update it — push, and revise its description if the change moved. Only when none exists is a new
one opened. Two pull requests for one set of changes split the review across places that each look
complete, and the second one silently discards whatever discussion had already happened on the
first.

### Every pull request is exactly one commit

The convention holds unchanged, and the argument for it is in **Adding things** above. What it
means in this workflow, on every subsequent push to a branch that already has a pull request:

1. fetch the branch's current history;
2. integrate the new changes;
3. **re-squash the whole branch onto its base** — one commit, never an accumulation;
4. rewrite the subject and body so they describe everything the branch now does;
5. keep the single `Co-authored-by: Claude <noreply@anthropic.com>` trailer;
6. `git push --force-with-lease` — never a bare `--force`, which discards whatever reached the
   remote while you were working;
7. update the existing pull request rather than opening another.

What the target branch must always see is one clean commit under the pull request branch, and never
a stack of four. A branch that has picked up a second commit is not finished — it is a branch
waiting to be squashed.

### The commit message

Every convention already established, restated here because this is where they are checked:
ASP.NET Core / Linux kernel style; **no conventional-commit prefixes** (`feat:`, `fix:`, `refactor:`,
`chore:`); a concise imperative subject; a blank line; an explanatory body long enough to justify
the change to someone reading it years later; American English; the `Co-authored-by` trailer kept;
and **no cloud session URL**, for the reason stated at length in **Adding things**.

### The pull request description

Held to the same standard as the commit message, because it outlives the conversation that produced
it. It explains what changed and — where it is not obvious — why; it reflects what the commit
actually contains rather than what was planned; it is written in American English; and it carries
**no cloud session URL**. When the single commit is re-squashed, the description is brought back
into line with it in the same round.

### Changelog

If a change deserves a changelog entry, it is written **in the same commit** — never in a follow-up
commit, which would put the branch back at two.

Note the precondition, because it is easy to read this rule as an instruction: **this repository has
no `Changelog.md` today**, so there is nothing to update and nothing to invent. The rule binds the
day one is added; until then, "deserves an entry" is never true. Do not create the file merely to
satisfy the rule.

### Before every push, verify

- a pull request exists for this branch — and if not, one is opened right after the first push;
- the branch carries **exactly one** commit;
- that commit follows every convention above;
- the `Co-authored-by` trailer is present, exactly once;
- no cloud session URL appears in the commit or in the pull request description;
- `CLAUDE.md` still describes the workflow actually being followed;
- `Changelog.md` is current, if and when the repository has one;
- an existing pull request was updated rather than a second one opened.

### Before considering a pull request complete, verify

- the implementation respects the Strategic Design;
- the README remains accurate;
- ADRs remain consistent;
- architecture tests still enforce the documented rules;
- no obsolete documentation remains;
- no architectural rule has silently drifted.

The repository values consistency over speed.

## Verification bar for any change

Release build with zero new warnings; the full suite green with Docker up; the
`Category!=Integration` filter green with Docker stopped — that is exactly CI's view; and if a
glossary term moved, a lexical sweep proving the abandoned form is gone everywhere except lines
that name it as abandoned.
