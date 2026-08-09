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

`[catalog].OutboxMessage`, mapped by `modelBuilder.MapOutbox()` in each module's own context — not
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
  (`NameFormJsonConverter`, `TitleJsonConverter`), registered as `JsonConverter` singletons and
  collected by the shared serializer.

Reading trusts the store: a payload the domain refuses is corruption, and it throws rather than
materializing a value that is not one — the repair belongs to a migration, exactly as it does for a
corrupt column.

**The stored type name is an address**: `"{FullName}, {AssemblySimpleName}"`, no version, so an
assembly bump orphans nothing. The flip side is a rule worth stating twice: **renaming or moving an
event type is a breaking change to every stored row that carries it.** Either the table drains
first, or the rename ships with a migration rewriting the stored names — and today only the first of
those exists, for the reasons [migrations.md](migrations.md) records.

**The same holds one level down, and it is easier to miss.** An event is a `record`, and each of its
positional parameters becomes a property name in the stored payload. Renaming
`AuthorRegistered(AuthorId, AuthorizedName)` to `(AuthorId, PreferredName)` leaves the address
intact and breaks every row all the same: the deserializer finds no `PreferredName` and hands the
handler a null the domain refuses. Type name and parameter names are one contract, and a rename of
either is the same decision — drain the table, or ship the migration.

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

Order is the contract because one contract carries a pair: `MemberBalanceChanged` announces the
amount before and the amount after, and two movements delivered out of order make the reader
compute a crossing that never happened — or miss the one that did, silently, which is the failure
mode that document exists to avoid. So the drain refuses to trade order for throughput. (This
paragraph once gave a different reason — *the debt settles before the copy is trapped* — and it
was wrong: the trap is synchronous, inside the return's own command, and no ordering of messages
ever sequenced it. The scenario it worried about is real and is handled where it can be — every
promotion re-asks the live balance, and the debt handler releases a copy trapped in the window.) A failing message blocks the head of its queue: the
failure is recorded on the row (`Attempts`, `Error`), the run stops, and the next run takes the
same head again. After five attempts the message is marked dead (`DeadOn`) and skipped, and the
queue moves again. A dead letter stays in the table because it is an operator's problem now, and a
problem that vanished is not solved.

The bookkeeping of a failure is written from a **fresh scope**: the scope the handler failed in may
hold half of that handler's changes, and saving the report there would save the half along with it.

Both failure moments also leave as **log lines** — a Warning while attempts remain, an Error when
the message dies — carrying the message id, the `EventId` and the type, never the payload. The
table records the failure, but nobody watches a table: the line is what an operator's alerting
hooks, and the `EventId` in it is the correlation token that follows an event across the
asynchronous boundary. A drain that delivered something says so once at Information; an idle tick
says nothing, because the drain runs every minute forever and a line per silence would bury the
lines that matter.

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

Two consequences bind every future host. **The mediator must be registered scoped**
(`options.ServiceLifetime = ServiceLifetime.Scoped`), discovered the hard way: the default
singleton resolves every handler from the root scope, which worked by accident while everything
shared one scope and breaks the drain's one-scope-per-message save — the handler would write to a
context nobody saves. And **the pipeline is declared in the same call**, as the mediator's own
ordered array:

```csharp
options.PipelineBehaviors =
[
    typeof(LoggingBehavior<,>),
    typeof(ValidationBehavior<,>),
    typeof(UnitOfWorkBehavior<,>),
];
```

Inline, because the source generator parses that very syntax and emits one closed registration per
message and behavior, honoring each behavior's constraints — and once per assembly, because a
second `AddMediator` carrying a different array would leave the generator to pick a winner. The
order is the guarantee: logging outermost so refusals still leave a line, validation before the
unit of work so a malformed command never writes.

The host also chooses the log sinks. The modules only speak `ILogger`: the shared registration
calls `AddLogging()` so a logger always resolves, and adds no provider — console, a collector,
OpenTelemetry are host decisions, exactly as the storage is.

## 8. Rules this imposes on handlers

* **No invariant rides on an event handler.** An event is handled later, in a transaction of its
  own; an invariant that waits is not an invariant. What must hold in the same instant as the
  command is written in the command handler, on the aggregates, directly.
* **Handlers are idempotent**, keyed by `EventId`: delivery is at-least-once.
* A handler never calls `SaveChangesAsync`; the drain's save carries its effects, and the events
  its aggregates raise become new rows in that same save.

## 9. The passage between modules

Everything above carries a domain event **within** its module. A second module reacting to it is a
different problem, and this section is its answer.

### What crosses

A **flattened contract** — a `record` of primitives, living in the publishing module's
`*.PublishedLanguage` project — and never a domain type. A domain event crossing a boundary would
export the model that raised it: the consumer would compile against `LoanId`, `CopyId` and whatever
those drag along, and every refactoring of the publisher's domain would become a change to somebody
else's code. That is the coupling the boundaries exist to prevent, and it is the reason the contract
is dull on purpose.

**The published language references nothing**, and that is load-bearing here rather than incidental.
It means the contract cannot implement a marker interface from the shared kernel — not
`IDomainEvent`, not the mediator's `INotification`, nothing. So the mediator cannot carry this hop:
`IDomainEventHandler<T>` is constrained to `IDomainEvent`, and a contract that satisfied it would be
a contract that referenced the kernel. Subscribers are resolved from the container directly, by the
contract's own type, and the genericity is the whole mechanism.

### Who translates

**The publisher.** A handler in the publishing module's infrastructure receives its own domain event
and publishes the contract. It cannot be the other way round: the consumer is forbidden from
referencing the publisher's `Domain`, so it could not name the event to subscribe to it.

The translation therefore happens inside the drain's delivery, on the publisher's side of the
boundary, and what leaves the module is already flat.

### How the consumer saves — the crux

The drain does **one message, one scope, one save**, and the save is on the *publisher's* context
(§4). A subscriber that wrote to its own module's `DbContext` would write to a context nobody saves:
the change would be tracked, never persisted, and nothing would report a failure. That is the exact
shape of silent loss the owned-collection defect had, and it is not a shape to build a mechanism on.

So a subscriber does not write. **It translates the contract into a command of its own module and
dispatches it.** `UnitOfWorkBehavior` then saves the right context, keyed by the command's declaring
assembly — the machinery that already exists, doing the thing it was built for. The consumer's
change lands in the consumer's transaction, decided by the consumer's own validators and handler,
and the publisher learns nothing about it.

The rule against nested dispatch is untouched: it forbids a **command** handler from depending on a
dispatcher, so that a use case cannot quietly become two. A subscriber is not a command handler, and
translating an external fact into a local use case is precisely its job.

### What this guarantees, and what it does not

**A failing subscriber blocks the head, and that is the point.** The exception travels up through
the delivery into the drain's own `try`, so the publisher's row is never marked processed and the
next run replays it. The mark never precedes the effect.

**The effect can precede the mark, though**, and there is no transaction spanning both: the
consumer's command commits in its own, and the publisher's mark in another. A crash between them
replays a fact the consumer has already acted on. This is the **at-least-once** promise §4 already
made, arriving where it was always going to bite, and **the subscriber owes idempotence** — by
`EventId`, or by a domain operation that is already idempotent. Declaring a copy lost is the second
kind: `Copy.DeclareLost` answers success for a copy already lost, which the aggregate decided for
its own reasons long before this mechanism existed.

### Two mechanisms that were refused

**An inbox per consumer** — a relay copying the contract into a table in the consumer's schema, in
the consumer's transaction, each module draining its own. It buys exactly-once per consumer, and it
costs a second table, a second drain per module and a relay whose own failure modes need the same
treatment all over again. The price is only worth paying when a subscriber cannot be made
idempotent, and none is: idempotence is already required of every handler here (§8), so the inbox
would buy a guarantee the handlers are obliged to provide anyway. It earns its place the day a
subscriber's effect is genuinely un-repeatable — money leaving the building, a message sent to a
person — and the note in §6 about the retry ladder is the model for how to bring it back.

**One transaction across both contexts** — enlisting the two `DbContext`s on a shared connection.
Genuinely atomic, and it dissolves the boundary it spans: two modules that commit together are one
module with two namespaces, and the day one of them moves out of the process the mechanism has to be
rebuilt from nothing. The separation of the contexts is the boundary; a transaction across it is a
hole in the boundary, whatever it buys.

## 10. Deferred, deliberately

Processed rows accumulate: a purge policy arrives with the real host, alongside the scheduler that
owns it. The Hangfire dashboard is a host concern too.

**The purge is a requirement and not only a convenience, and it took another document to notice.**
A delivered row keeps its payload, and a payload is whatever the event carried — which for Members
means a name, a set of contact details, a guardian. Nothing else bounds that copy: the aggregate can
be emptied on request, and those rows would still hold what it used to say. So the window is what
makes erasure elsewhere real, and a host that never sets one has a second store of personal data it
did not decide to keep. How long the window is stays the host's call, as the scheduler and the sinks
are; that there is one is not. `docs/tactical-design-members.md` §10 records the other side.
