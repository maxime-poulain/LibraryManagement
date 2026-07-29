# The transactional outbox

How a domain event travels from the aggregate that raised it to the handlers that react, and the
decisions that shaped the road. Boundaries come from [strategic-design.md](strategic-design.md);
this document records a technical choice and its reasons, so that revisiting it is a decision
rather than an accident.

## 1. The guarantee

A domain event is not executed; it is written down. `OutboxInterceptor` runs inside
`SaveChangesAsync`, serializes every event the tracked aggregates raised, and adds one row per
event to the **same `DbContext`** — so the row and the business change leave in one save, therefore
one implicit transaction. The store can never hold the fact without the announcement, nor the
announcement without the fact. That sentence is the entire pattern; everything below is
consequences.

Publishing after the save — the obvious alternative — is two transactions, and the second is free
to fail alone: a fact the system never announces, or with the order reversed, an announcement of
nothing. That is the exact failure the pattern exists to remove, and why the interceptor sits on
`SavingChanges` and not `SavedChanges`.

## 2. One table per module

`[catalog].OutboxMessages`, mapped by `modelBuilder.MapOutbox()` in each module's own context — not
a central table. This is forced, not chosen: the row must be written by the same context as the
change to share its transaction, and a central table would be a second context, a second
transaction, and the problem back again. A module that forgets the mapping fails loudly on the
first save that raises an event, with the message naming the missing type.

The row (`OutboxMessage`) is deliberately anemic — infrastructure's own bookkeeping, not a domain
object. `Id` is an identity column because the queue needs strict insertion order and a version 7
UUID only orders to the millisecond; `EventId` carries the event's identity and is unique, so the
same occurrence can never be *stored* twice.

## 3. Serialization

Events carry the domain's own types — identifiers with closed constructors, value objects that
validate on creation — and none of that bends for a serializer, any more than it did for Entity
Framework. Infrastructure owns converters instead, the exact mirror of the store's value
conversions:

* One generic `EntityIdJsonConverterFactory` covers every identifier there is or will be, reading
  back through `FromValue` — the `static abstract` member, reachable from a generic without
  reflection per call.
* Each module contributes one small converter per value object its events carry
  (`PersonNameJsonConverter`, `TitleJsonConverter`), registered as `JsonConverter` singletons and
  collected by the shared serializer.

Reading trusts the store: a payload the domain refuses is corruption, and it throws rather than
materializing a value that is not one — the repair belongs to a migration, exactly as it does for a
corrupt column.

**The stored type name is an address**: `"{FullName}, {AssemblySimpleName}"`, no version, so an
assembly bump orphans nothing. The flip side is a rule worth stating twice: **renaming or moving an
event type is a breaking change to every stored row that carries it.** Either the table drains
first, or the rename ships with a migration rewriting the stored names.

## 4. Delivery

`OutboxProcessor<TContext>` drains one module's table: **one message, one scope, one save**. The
handler runs, then `ProcessedOn` and everything the handler changed — including new outbox rows for
events the handler's own aggregates raised — leave in a single `SaveChangesAsync`. Within the
module that is effectively exactly-once: a message is never marked without its effects, never takes
effect without its mark. Chains drain naturally, one generation per message, within the same run.

Across the wider world the promise is **at-least-once**, and `IDomainEvent.EventId` is what a
handler deduplicates by. The unique index keeps a duplicate from ever being stored; a duplicate
*delivery* remains possible and is the handler's problem by contract.

Delivery goes through `IDomainEventPublisher`, and its mediator adapter is the only file in the
solution that names a mediator — by the `object` overload, deliberately, so handlers are resolved
on the event's runtime type rather than on the interface the variable happens to have. Replacing
the delivery — a bus, some day — is one registration.

## 5. Failure: order is the contract

Circulation's design leans on causal order — the debt settles before the copy is trapped — so the
drain refuses to trade order for throughput. A failing message blocks the head of its queue: the
failure is recorded on the row (`Attempts`, `Error`), the run stops, and the next run takes the
same head again. After five attempts the message is marked dead (`DeadOn`) and skipped, and the
queue moves again. A dead letter stays in the table because it is an operator's problem now, and a
problem that vanished is not solved.

The bookkeeping of a failure is written from a **fresh scope**: the scope the handler failed in may
hold half of that handler's changes, and saving the report there would save the half along with it.

## 6. The retry is the heartbeat — a decision record

The drain is a recurring Hangfire job per module, `Cron.Minutely()`, and that tick is also the
retry: a transient failure waits at most a minute, a poisonous message dies in about five.

A nearer retry — the processor's blockage handed to `IBackgroundJobClient.Schedule` with a backoff
ladder — **was built and then removed**, and the reasons are the record:

* The ladder (15 s, 30 s, 60 s) converged to the tick's own period by its third step. The mechanism
  bought seconds, on handlers that are asynchronous by design — everything a librarian can see at
  the desk is synchronous in the command, and the strategic design's own boundary test says a few
  seconds of disagreement is precisely what nobody notices.
* The schedule was not transactional with the failure it reacted to, so the recurring tick stayed
  load-bearing regardless. Two mechanisms answering one question, of which only one could be relied
  on, is one mechanism too many.

It would earn its place back the day the heartbeat slows far below a minute, or a handler's latency
becomes something the business feels. If it returns: schedule with backoff, never a bare enqueue —
an immediate retry of a deterministic failure burns all five attempts in a second, dead-lettering a
message a transient fault would have released — and reschedule *the drain*, never a per-message
job: the failed message is always the head, so retrying it and draining are the same act. The
processor already reports everything the decision needs (`OutboxDrainOutcome`).

## 7. The scheduler lives in the composition root

No project under `src/` references Hangfire, for the same reason none names a database provider:
the processor is a plain class, and what puts it on a clock is the host's decision. The host's
whole surface is `OutboxJobs` — one method per module, `[DisableConcurrentExecution]` so a tick and
a late run never drain the same table at once — plus the storage configuration, in the `hangfire`
schema beside the module schemas, owned by infrastructure the way `sys` is.

One consequence binds every future host, discovered the hard way: **the mediator must be registered
scoped** (`AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped)`). The default
singleton resolves every handler from the root scope, which worked by accident while everything
shared one scope and breaks the drain's one-scope-per-message save — the handler would write to a
context nobody saves.

## 8. Rules this imposes on handlers

* **No invariant rides on an event handler.** An event is handled later, in a transaction of its
  own; an invariant that waits is not an invariant. What must hold in the same instant as the
  command is written in the command handler, on the aggregates, directly.
* **Handlers are idempotent**, keyed by `EventId`: delivery is at-least-once.
* A handler never calls `SaveChangesAsync`; the drain's save carries its effects, and the events
  its aggregates raise become new rows in that same save.

## 9. Deferred, deliberately

Processed rows accumulate: a purge policy arrives with the real host, alongside the scheduler that
owns it. Cross-module integration events — flattened contracts, not domain types — are a different
concern for the day two modules exist; this outbox carries *domain* events within their module.
The Hangfire dashboard is a host concern too.
