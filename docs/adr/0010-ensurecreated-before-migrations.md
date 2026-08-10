# ADR-0010 — `EnsureCreated` now; migrations at the first host

- **Status**: Resolved — the trigger fired at the first host; migrations exist, and
  [`migrations.md`](../migrations.md) describes them
- **Date**: 2026-07-31
- **Resolved**: 2026-08-09
- **Authority**: [`migrations.md`](../migrations.md) — the whole document

> **What happened.** The deferral below named its own end: *the first host, or the first database
> that holds data, whichever comes first.* The host arrived, and with it the legitimate place for a
> provider that §Context says was the real obstacle — a migrations project per module, beside the
> host rather than inside it, exactly the shape §Consequences recorded in advance. The record stays
> as written, because what a deferral predicted is worth reading against what it got.

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

## What arrived, against what this predicted

The shape held: one project per module under `src/Host/`, a design-time factory each,
`MigrationsAssembly()` pointing at itself, a history table per schema. Two things the record did not
have.

**The provider call needed one home, and the first attempt gave it five.** The host, five test
fixtures and the design-time factories all have to say `UseSqlServer` with the same two settings,
and the history table fails *silently* when one caller forgets it — so the call became a named
extension per module. But the extension itself was then written out five times, which made five
places to forget rather than none: `migrations.md` claimed the rule was spelled once and the code
spelled it per module. The duplication gate on the first pull request is what surfaced it, and the
rule now lives in one project the five reference. A shared thing recognised one level too late is
the ordinary shape of this mistake.

**The workaround had spread.** §Consequences called `CreateTablesAsync` "three lines in one test";
by the time migrations arrived, three more composition tests had copied it. A workaround that is
cheap to leave in place is a workaround that gets copied, and the count is the honest measure of
what deferring cost.
