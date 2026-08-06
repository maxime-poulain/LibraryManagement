# ADR-0002 — One `DbContext` and one schema per module; no foreign key crosses a schema

- **Status**: Accepted
- **Date**: 2026-07-27
- **Authority**: [`strategic-design.md`](../strategic-design.md) §10

## Context

Five modules share one database ([ADR-0001](0001-modular-monolith-over-services.md)). A single
`DbContext` over five schemas would compile and would work. It would also mean that one
`DbSet<Copy>` referenced from a Circulation handler ends the separation, silently, with nothing
failing and no test turning red.

Modules genuinely reference each other's rows: a `Copy` names an `EditionId` that Catalog issued, a
`MemberAccount` is keyed by the `MemberId` Members issued. The question was whether the database
should enforce those references.

## Decision

One `DbContext` per module, one schema per module, and **no foreign key ever crosses a schema**. A
module referencing another's row holds its identifier and nothing else, redeclared as its own type —
Holdings' `EditionId` and Catalog's carry the same `Guid` and are different types.

Cross-schema questions are asked through the owning module's published language and answered by its
handler, which is also what makes the answer a sentence: *edition 3f2a is not cataloged* is something
a foreign key violation cannot say.

## Consequences

The separation is structural. A Circulation handler cannot reach a `Copy` because its context has
never heard of one, and that is a compile error rather than a code review.

**The database will not catch a dangling reference, and that is accepted deliberately.** Holdings
§10 records the concrete case: the day Catalog gains a merge operation, every `Copy` holding the
absorbed `EditionId` points at a record that no longer answers, and nothing reports it. The answer is
an event the downstream consumes, not a constraint — an integrity constraint across modules is a
coupling the compiler cannot see, and removing that net is the price of the boundary rather than an
oversight in it.

Each module maps its own outbox table into its own schema, which is forced by
[ADR-0006](0006-transactional-outbox-per-module.md) rather than chosen here.

Two contexts over one database do not compose for free. `EnsureCreated` builds the database for the
first context and answers "already there" for the second, leaving that module's tables unbuilt —
[ADR-0010](0010-ensurecreated-before-migrations.md) carries the workaround and its expiry.

## Alternatives rejected

**One `DbContext` with five schemas.** Cheaper to configure and it dissolves the boundary at the
first convenient `Include`. The failure mode is the one this repository most wants to avoid: nothing
breaks, so nobody learns.

**A database per module.** It would enforce the separation absolutely and would cost cross-schema
read models ([`strategic-design.md`](../strategic-design.md) §7 puts search and statistics on views
over other modules' schemas), plus five connection strings and five backup policies for a system that
runs in one process.
