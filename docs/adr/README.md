# Architecture decision records

One record per decision that would be expensive to reverse, and that a reader would otherwise have
to reconstruct from the code.

## What belongs here, and what does not

The design documents in `docs/` are the **authority**: `strategic-design.md` decides boundaries,
each `tactical-design-*.md` decides one context's aggregates, `outbox.md` and `migrations.md` decide
two technical mechanisms. They are long because the argument is the point.

An ADR is not a summary of one of those. It is the **decision record**: what was decided, when, what
it costs, and what was rejected — in a form that a reader scanning thirteen titles can navigate. Where
a decision is argued at length elsewhere, the record says so and points there rather than restating
it. When a record and its authority disagree, the authority wins and the record is the bug.

A decision earns a record when reversing it would touch more than one module, or when the reason for
it is invisible in the code that results. A decision that only shapes one aggregate belongs in that
context's tactical design.

## Status vocabulary

**Accepted** — in force, and the code holds to it. **Superseded by ADR-N** — replaced; the record
stays, because a decision that vanished is a decision nobody can learn from. **Deferred** — the
decision is made *not* to decide yet, and the record names the trigger that reopens it.

There are no *Proposed* records: this repository decides on paper before it builds, so a record is
written when the decision is taken.

## The records

| # | Decision | Status |
|---|---|---|
| [0001](0001-modular-monolith-over-services.md) | A modular monolith, not services | Accepted |
| [0002](0002-one-dbcontext-and-schema-per-module.md) | One `DbContext` and one schema per module; no foreign key crosses a schema | Accepted |
| [0003](0003-command-is-the-unit-of-consistency.md) | A command is the unit of consistency | Accepted |
| [0004](0004-result-over-exceptions.md) | `Result` for refusals, exceptions for faults | Accepted |
| [0005](0005-mediator-source-generator.md) | Mediator's source generator, not MediatR | Accepted |
| [0006](0006-transactional-outbox-per-module.md) | Domain events through a transactional outbox, one table per module | Accepted |
| [0007](0007-published-language-only.md) | Modules speak only through published languages of primitives | Accepted |
| [0008](0008-subscriber-dispatches-its-own-command.md) | A cross-module subscriber dispatches its own command | Accepted |
| [0009](0009-inverted-balance-port.md) | Circulation declares the balance port; Charges implements it | Accepted |
| [0010](0010-ensurecreated-before-migrations.md) | `EnsureCreated` now; migrations at the first host | Deferred |
| [0011](0011-architecture-rules-are-tests.md) | Architectural rules are executable tests | Accepted |
| [0012](0012-american-english-binding-glossary.md) | American English, and the glossary is binding | Accepted |
| [0013](0013-composition-root-in-the-tests.md) | No runnable host; the composition root lives in the tests | Accepted |
