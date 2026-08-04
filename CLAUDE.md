# CLAUDE.md

Session context for AI-assisted work in this repository. The documents under `docs/` are the
authority on the domain and the architecture; this file is the map to them, plus what a fresh
session needs to build, test and contribute without rediscovering the rules the hard way.

## What this is

A library management system for the staff of a single public library: a modular monolith on
.NET 10, organised as five DDD bounded contexts — Catalog, Holdings, Circulation (core), Members,
Charges — over a small technical shared kernel. The project is design-first: every context is
decided on paper in `docs/` before it is coded, and the documents record rejected alternatives,
not just outcomes.

**State.** Shared kernel, Catalog and Holdings are implemented and tested. Circulation, Members
and Charges exist as design only. There is no runnable host — the composition root lives in the
composition tests — and no EF migrations, deliberately (`docs/migrations.md`).

## The documents are the authority

Read the relevant one before modeling anything. When code and a document disagree, one of them
is a bug: fix the pair in the same change.

| Document | Decides |
|---|---|
| `docs/strategic-design.md` | Boundaries, subdomains, context map. §4 is the **glossary — the binding ubiquitous language**. §10 is the codebase rules. |
| `docs/tactical-design-circulation.md` | Circulation's aggregates, invariants and moments. Not yet implemented. |
| `docs/tactical-design-holdings.md` | Holdings' aggregate and moments. Implemented; §10 records what building it taught. |
| `docs/outbox.md` | Domain events: same-save storage, drain, failure semantics, and what renames break. |
| `docs/migrations.md` | Why `EnsureCreated` for now, the shape migrations will take, and the trigger for the switch. |

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
forked pull requests.

## Layout

```
src/Shared/       Technical kernel: Entity, ValueObject, Result, CQS, pipeline behaviors,
                  outbox, audit. Building blocks only — never business concepts.
src/<Module>/     One bounded context: Domain / Application / Infrastructure /
                  PublishedLanguage (what the module says to the ones downstream of it).
tests/            Mirrors src/, plus Architecture.Tests (reflection rules over the built
                  assemblies) and Composition.Tests (whole pipeline, two modules, Hangfire).
docs/             The design. Authoritative.
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
- **Renaming a stored event type — or any positional parameter of an event `record` — breaks
  every stored payload** (`docs/outbox.md` §3). Free only while the outbox tables are empty.

**Contracts.**
- A query answers with a `Dto`-suffixed type (architecture rule).
- An aggregate maps through `AggregateRootConfiguration<,>` (architecture rule — it carries the
  key conversion and the `rowversion` concurrency token, whose omission fails silently).
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

## Verification bar for any change

Release build with zero new warnings; the full suite green with Docker up; the
`Category!=Integration` filter green with Docker stopped — that is exactly CI's view; and if a
glossary term moved, a lexical sweep proving the abandoned form is gone everywhere except lines
that name it as abandoned.
