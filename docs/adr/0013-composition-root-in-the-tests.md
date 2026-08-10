# ADR-0013 — No runnable host; the composition root lives in the tests

- **Status**: Superseded by [ADR-0015](0015-the-first-host.md) — the host exists; the
  composition root moved into it, and the two job classes below moved with it
- **Date**: 2026-07-29
- **Authority**: [`outbox.md`](../outbox.md) §7,
  [`strategic-design.md`](../strategic-design.md) §10

## Context

Several things every module depends on are, correctly, **not a module's decision**: which database
provider serves the tables, what puts the outbox drain on a clock, where log lines go, how the schema
comes into being. Each is a host decision, and a module that made one would carry an engine or a
scheduler into a place that has a written rule against naming either.

The project is design-first and has no user interface. Building an ASP.NET Core host before the
domain was finished would have meant deciding a presentation for five contexts that did not exist.

## Decision

There is no runnable host. **The composition root lives in
`tests/Composition/LibraryManagement.Composition.Tests`**, which is a real one: it registers all five
modules, the mediator with its pipeline, Hangfire with the drain jobs, and a SQL Server from
Testcontainers.

Two rules keep the modules host-agnostic, and both are checked:

- **No module names a database provider.** Every infrastructure project references
  `Microsoft.EntityFrameworkCore.Relational` and no provider. `Microsoft.EntityFrameworkCore.SqlServer`
  belongs to the composition root and to the migrations projects that arrived with it under
  `src/Host/` — the rule was written as "nothing under `src/`" while `src/` held only modules, and
  what it always meant is that a *module* stays engine-agnostic. See
  [ADR-0010](0010-ensurecreated-before-migrations.md) and [`migrations.md`](../migrations.md) §2 for
  why the migrations sit beside the host rather than inside the modules they describe.
- **Nothing under `src/` names a scheduler.** `OutboxProcessor<TContext>` is a plain class. Hangfire
  appears only in the composition tests, whose whole surface is `OutboxJobs` — one method per module,
  `[DisableConcurrentExecution]` so a tick and a late run never drain the same table at once.

Each module exposes one `Add<Module>Module` extension, and the host calls all five.

## Consequences

The composition is **exercised** rather than asserted. `TwoModulesTests` and the integration-event
tests run the whole pipeline — command, validation, unit of work, outbox write, drain, cross-module
subscriber, second module's transaction — against a real database, which is a stronger claim than a
host that merely starts.

Everything a host must know is written down rather than discovered: the mediator registered
**scoped**, the pipeline declared inline in the same call, the storage in a `hangfire` schema beside
the module schemas, `AddLogging()` with no provider so the host picks its sinks. Those are recorded in
[`outbox.md`](../outbox.md) §7 precisely because they were learned the hard way and would otherwise
have to be relearned.

**What is genuinely missing is named, not glossed over.** There is no HTTP surface, no authentication,
no Hangfire dashboard, no outbox purge policy — and no migrations, which
[ADR-0010](0010-ensurecreated-before-migrations.md) ties to this same trigger. `ICurrentEmployee` has
`UnattributedEmployee` standing in for the authentication a host would supply.

**Adding a host is the trigger for several deferred decisions at once** — migrations, the retention
window that makes erasure real, the purge, the log sinks. That clustering is a reason to expect the
first host to be a larger change than it looks.

The read-side composition it will own is already designed:
[`strategic-design.md`](../strategic-design.md) §10 settles that a page starting from an identifier
in hand dispatches each module's published query at the edge and assembles a view model of its own,
deciding nothing — so the host builds against a decided shape rather than inventing one.

## Alternatives rejected

**Build the ASP.NET Core host now.** It would decide a presentation for contexts that did not exist
yet, and every module built afterwards would be shaped by an endpoint written before its domain.

**A console host that only runs the drain.** It would satisfy the letter of "there is a host" and
prove less than the composition tests already do, since it exercises no command path.
