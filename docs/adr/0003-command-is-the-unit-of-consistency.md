# ADR-0003 — A command is the unit of consistency

- **Status**: Accepted
- **Date**: 2026-07-27
- **Authority**: [`strategic-design.md`](../strategic-design.md) §10

## Context

Something has to decide where a transaction begins and ends. Left to the handlers, each one calls
`SaveChangesAsync` when it judges the work finished, and the boundary becomes a convention that holds
only as long as everyone remembers it.

Five modules also mean five implementations of `IUnitOfWork` registered against one interface, where
the last registration answers for all of them. A unit of work therefore **cannot be resolved by type
alone**, and that was an open question when the shared infrastructure was written.

## Decision

The command is the transaction. `UnitOfWorkBehavior` sits in the pipeline and writes **once**, on
success, through the unit of work of the module the command belongs to — resolved by the assembly
that declares the command, since a command belongs to exactly one module.

Handlers and repositories never call `SaveChangesAsync`. Repositories only track; nothing reaches the
database before the behavior saves.

Two rules hold the shape:

- **A command handler never depends on `ICommandDispatcher` or `IQueryDispatcher`** — no nested
  dispatch, so a use case cannot quietly become two, and the assembly the unit of work is keyed on
  stays unambiguous. An architecture rule enforces it.
- **No invariant rides on an event handler.** Anything that must be true in the same instant as the
  command is written in the command handler, on the aggregates, directly.

## Consequences

A refused command writes nothing, without anyone arranging that. Validation runs before the unit of
work in the pipeline order, so a malformed command never reaches a store.

**There is no explicit transaction, and none is needed.** One `SaveChangesAsync` is already atomic,
and no command writes twice. `IUnitOfWork` records the condition that would earn a transaction back —
a command writing to two contexts — and nothing meets it.

Crossing two aggregates inside one transaction is permitted and used: a return closes the `Loan` and
traps the copy in the edition's `HoldQueue` in one breath, because the business requires both to
agree at every instant. That is a considered exception, argued in
[`tactical-design-circulation.md`](../tactical-design-circulation.md) §5, not a relaxation of the
rule.

An event raised by a command that then fails is never stored, since the outbox row and the change
share the save. This is what made `RenewalRefused` unpublishable, and
[`tactical-design-circulation.md`](../tactical-design-circulation.md) §10 records why it turned out
not to be wanted.

## Alternatives rejected

**Handlers save themselves.** One forgotten call is a silently dropped write. One extra call is a
partial commit that no test would catch.

**An ambient transaction scope per request.** It makes the boundary the *request* rather than the
command, which is a presentation concern deciding a domain one — and it would quietly permit the
nested dispatch the rule above forbids.
