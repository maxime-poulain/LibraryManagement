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
Circulation judges it against its own threshold and cancels the borrower's holds. There is
no runnable host — the composition root lives in the composition tests — and no EF migrations,
deliberately (`docs/migrations.md`).

## The documents are the authority

Read the relevant one before modeling anything. When code and a document disagree, one of them
is a bug: fix the pair in the same change.

| Document | Decides |
|---|---|
| `docs/strategic-design.md` | Boundaries, subdomains, context map. §4 is the **glossary — the binding ubiquitous language**. §10 is the codebase rules. |
| `docs/tactical-design-circulation.md` | Circulation's aggregates, invariants and moments. Desk moments, the daily process and both directions of the Charges cycle implemented; §10 records what building each taught. |
| `docs/tactical-design-holdings.md` | Holdings' aggregate and moments. Implemented; §10 records what building it taught. |
| `docs/tactical-design-members.md` | Members' aggregate and moments. Implemented; §10 records what building it taught. |
| `docs/tactical-design-charges.md` | Charges' aggregate, invariants and moments. Implemented; §10 records what building it taught, including the one place the mapping had to depart from the rest of the solution. |
| `docs/outbox.md` | Domain events: same-save storage, drain, failure semantics, the cross-module passage (§9), and what renames break. |
| `docs/migrations.md` | Why `EnsureCreated` for now, the shape migrations will take, and the trigger for the switch. |
| `docs/adr/` | Thirteen decision records — what was decided, when, what it costs, what was rejected. Navigation, not argument: where a decision is argued at length above, the record points there rather than restating it. Start at `docs/adr/README.md`. |
| `README.md` | The public face: state, context map, build and test, the rules, and the index to all of the above. It summarizes and never decides — when it disagrees with a document here, the document wins. |

## Build and test

```bash
dotnet build LibraryManagement.slnx --configuration Release
dotnet test  LibraryManagement.slnx --configuration Release --no-build   # full suite — needs Docker
dotnet test  LibraryManagement.slnx --configuration Release --no-build \
             --filter "Category!=Integration"                            # CI's view — no Docker
```

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
tests/            Mirrors src/, plus Architecture.Tests (reflection rules over the built
                  assemblies) and Composition.Tests (whole pipeline, all five modules and
                  their five outbox drains, Hangfire).
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
- Nothing under `src/` names a database provider (EF `Relational` only) or a scheduler (no
  Hangfire). Both are host decisions; today "the host" is the composition tests.

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
- **A module**: copy the Catalog/Holdings shape, then wire *all* of — `LibraryManagement.slnx`;
  a `ProjectReference` in Architecture.Tests **and** one pinned handler in
  `CommandHandlerRulesTests` (the scan only sees referenced assemblies, and green over nothing
  reads exactly like green over everything); `Add<Module>Module` in the composition tests plus a
  drain method in `OutboxJobs`; `MapOutbox()` in the module's `DbContext`.
- **Prose**: XML docs and comments state the constraint and the alternative that was rejected —
  never what the next line does. Commit subjects are plain sentences, not conventional-commit
  prefixes; the log reads as a narrative and should stay one.
- **Commit trailers**: exactly one, `Co-authored-by: Claude <noreply@anthropic.com>`, and **never a
  session URL**. A `Claude-Session:` link points at a conversation nobody outside it can open, and
  it dates the moment rather than the change — the log is read years later by someone reconstructing
  why, and a dead link is worse than no link because it looks like it should work. What the commit
  has to justify itself with is its own message. The attribution stays because authorship is a fact
  about the change; the transcript is not.

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

Every Domain Service must be explicitly suffixed with `DomainService`.

Avoid the generic `*Service` suffix for domain concepts.

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

Before considering a Pull Request complete, verify:

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
