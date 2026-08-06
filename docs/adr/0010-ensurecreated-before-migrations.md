# ADR-0010 — `EnsureCreated` now; migrations at the first host

- **Status**: Deferred — the trigger is named below
- **Date**: 2026-07-31
- **Authority**: [`migrations.md`](../migrations.md) — the whole document

## Context

Schemas have to come into being somehow. Today `EnsureCreatedAsync` builds them, in the two
`SqlServerFixture` classes and in the composition tests — nowhere else, since nothing under `src/`
ever creates a table.

Two reasons keep migrations out, and the second outlives the first.

**Nothing is deployed.** No host, no database holding data, no environment whose schema must survive
a change. Generating them now would mean carrying a migration through every rename of Circulation,
Members and Charges for a schema nobody runs — the ubiquitous-language pass alone would have shipped
one per renamed type.

**A migration names a provider, and a module may not.** Every infrastructure project references
`Microsoft.EntityFrameworkCore.Relational` and no provider, deliberately. A migration is
provider-specific down to its generated code — `nvarchar(max)`, `rowversion`, `IDENTITY` — so putting
one inside a module would put SQL Server inside a module that has a written rule against naming an
engine.

## Decision

No migrations yet. Fixtures build the schema with `EnsureCreated`.

**The trigger is explicit: the first host, or the first database that holds data, whichever comes
first.** A deferral with no trigger is not a decision but a habit.

## Consequences

**The tests validate a schema built by a different mechanism than a deployment would use.** A
migration that drifted from the model would pass every test in this repository. This is the cost that
eventually forces the change, and it is stated rather than discovered.

`EnsureCreated` creates the *database* and then answers "already there" for a second context over it,
leaving that module's tables unbuilt — so `TwoModulesTests` reaches for the relational creator's
`CreateTablesAsync` directly. Three lines in one test today; migrations remove the workaround.

One rule already assumes migrations exist. [`outbox.md`](../outbox.md) §3 requires that renaming a
stored event type either drain the table first **or** ship with a migration rewriting the stored
names — and today only the first half is available. Survivable exactly as long as nothing is
deployed.

**The shape they will take is recorded now**, so the day it happens is an execution rather than an
investigation: one migrations project per module
(`LibraryManagement.<Module>.Migrations.SqlServer`), each holding an
`IDesignTimeDbContextFactory<T>` and pointing the runtime at itself with `MigrationsAssembly()`. And
two contexts on one database need **two history tables** — left to the default, each module reads the
other's rows as migrations of its own it has never applied, which fails in a way that looks like
corruption rather than like a configuration mistake.

## Alternatives rejected

**Migrations now.** Provider inside a module, or a migrations project per module maintained through
months of renames for a database nobody runs.

**A migrations project per module, empty until needed.** The structure without the benefit, and it
would still have to be kept in step with every rename to stay meaningful.
