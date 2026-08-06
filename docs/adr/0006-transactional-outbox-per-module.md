# ADR-0006 — Domain events through a transactional outbox, one table per module

- **Status**: Accepted
- **Date**: 2026-07-29
- **Authority**: [`outbox.md`](../outbox.md) — the whole document

## Context

Aggregates raise domain events. Publishing them in-process after the save is two operations that can
fail independently: a fact the system never announces, or — with the order reversed — an announcement
of something that did not happen.

## Decision

A domain event is **not executed; it is written down**. `OutboxInterceptor` runs inside
`SaveChangesAsync`, serializes every event the tracked aggregates raised, and adds one row per event
to the **same `DbContext`** — so the row and the business change leave in one save, therefore one
implicit transaction.

One table per module, `[<module>].OutboxMessage`, mapped by `modelBuilder.MapOutbox()`. This is
forced rather than chosen: the row must be written by the same context as the change in order to
share its transaction, so a central table would be a second context and the problem back again.

`OutboxProcessor<TContext>` drains one module's table: **one message, one scope, one save**.

## Consequences

The store can never hold the fact without the announcement, nor the announcement without the fact.
That sentence is the whole pattern; everything else is consequence.

**Within a module, delivery is effectively exactly-once** — the handler's effects and the
`ProcessedOn` mark leave in one save. **Beyond it, at-least-once**, and handlers deduplicate by
`IDomainEvent.EventId`.

**Order is the contract.** Circulation's design leans on causal order — the debt settles before the
copy is trapped — so a failing message blocks the head of its queue rather than being skipped. Five
attempts, then dead-lettered and left in the table, because a problem that vanished is not solved.

**Renaming a stored event type breaks every stored row that carries it**, and so does renaming any
positional parameter of an event `record`, since each becomes a property name in the payload. Free
only while the tables are empty — which they are, and which
[ADR-0010](0010-ensurecreated-before-migrations.md) is the reason for.

The scheduler is not named anywhere under `src/`. What puts the processor on a clock is the host's
decision, exactly as the database provider is — see
[ADR-0013](0013-composition-root-in-the-tests.md).

**Processed rows accumulate, and the purge is a requirement rather than housekeeping.** A delivered
row keeps its payload, and a Members payload is a name, contact details, a guardian. Nothing else
bounds that copy, so a host that sets no retention window has a second store of personal data it did
not decide to keep. [`outbox.md`](../outbox.md) §10 and
[`tactical-design-members.md`](../tactical-design-members.md) §10 record the two halves.

## Alternatives rejected

**Publish after the save.** Two transactions, the second free to fail alone. The exact failure the
pattern removes, which is why the interceptor sits on `SavingChanges` and not `SavedChanges`.

**One central outbox table.** A second context, a second transaction, and the atomicity gone.

**A nearer retry ladder**, built and then removed. Its steps (15 s, 30 s, 60 s) converged on the
minutely tick by the third, it was not transactional with the failure it reacted to, and the
recurring tick stayed load-bearing regardless — two mechanisms answering one question, of which only
one could be relied on. [`outbox.md`](../outbox.md) §6 records what would earn it back.
