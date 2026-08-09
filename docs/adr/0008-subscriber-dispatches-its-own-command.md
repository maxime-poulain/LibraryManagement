# ADR-0008 — A cross-module subscriber dispatches its own command

- **Status**: Accepted
- **Date**: 2026-08-05
- **Authority**: [`outbox.md`](../outbox.md) §9

## Context

[ADR-0007](0007-published-language-only.md) settles *what* crosses a module boundary. What remained
was how the receiving module **writes** what it decides.

The drain does one message, one scope, one save — and the save is on the **publisher's** context
([ADR-0006](0006-transactional-outbox-per-module.md) §4). A subscriber that wrote to its own module's
`DbContext` inside that scope would write to a context nobody saves: the change tracked, never
persisted, and nothing reporting a failure. That is the same shape of silent loss as the owned-key
defect recorded in [`tactical-design-circulation.md`](../tactical-design-circulation.md) §10, and not
a shape to build a mechanism on.

## Decision

**A subscriber does not write.** It translates the contract into a command of its own module and
dispatches it. `UnitOfWorkBehavior` then saves the right context, keyed by the command's declaring
assembly — the machinery [ADR-0003](0003-command-is-the-unit-of-consistency.md) already built, doing
what it was built for.

The consumer's change lands in the consumer's transaction, decided by the consumer's own validators
and handler, and the publisher learns nothing about it.

A refused command is turned into an exception (`Refusal.Throw`), because the drain's contract is that
a throwing handler leaves the message unmarked.

## Consequences

This does not violate the rule against nested dispatch. That rule forbids a **command handler** from
depending on a dispatcher, so a use case cannot quietly become two. A subscriber is not a command
handler, and translating an external fact into a local use case is precisely its job.

**A failing subscriber blocks the head of the publisher's queue, and that is the point.** The
exception travels up into the drain's own `try`, the row is never marked processed, and the next run
replays it. The mark never precedes the effect.

**The effect can precede the mark**, and no transaction spans both: the consumer's command commits in
its own, the publisher's mark in another. A crash between them replays a fact the consumer already
acted on. This is the at-least-once promise arriving where it was always going to bite, and **the
subscriber owes idempotence** — by `EventId`, or by a domain operation that is already idempotent.

Both kinds are in use. `Copy.DeclareLost` answers success for a copy already lost, which the aggregate
decided for its own reasons long before this mechanism existed. Charges deduplicates on the aggregate
instead — and its first attempt is the caution worth keeping: reading the memory off the outstanding
charges expired the guarantee the moment the desk settled one, so the account keeps a memory of
priced loans that outlives its charges. Aggregate-side idempotence must rest on state that nothing
legitimately empties — [`tactical-design-charges.md`](../tactical-design-charges.md) §10 records the
correction.

## Alternatives rejected

**An inbox per consumer** — a relay copying the contract into a table in the consumer's schema, in the
consumer's transaction. It buys exactly-once per consumer and costs a second table, a second drain per
module, and a relay whose own failure modes need the same treatment again. Idempotence is already
required of every handler, so the inbox would buy a guarantee the handlers are obliged to provide
anyway. It earns its place the day a subscriber's effect is genuinely un-repeatable — money leaving
the building, a message sent to a person.

**One transaction across both contexts**, enlisting the two `DbContext`s on a shared connection.
Genuinely atomic, and it dissolves the boundary it spans: two modules that commit together are one
module with two namespaces, and the day one moves out of the process the mechanism is rebuilt from
nothing.
