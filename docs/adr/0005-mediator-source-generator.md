# ADR-0005 — Mediator's source generator, not MediatR

- **Status**: Accepted
- **Date**: 2026-07-27
- **Authority**: [`outbox.md`](../outbox.md) §7, `Directory.Packages.props`

## Context

The CQS pipeline needs a dispatcher: something that takes a command, finds its handler, and runs the
behaviors around it. MediatR is the default choice in this part of the .NET world and resolves
handlers reflectively at runtime.

## Decision

`Mediator` (the source-generator implementation), referenced as `Mediator.Abstractions` in `src/` and
`Mediator.SourceGenerator` only where the pipeline is composed.

The modules do not depend on it directly. `ICommandDispatcher` and `IQueryDispatcher` are this
solution's own abstractions in the shared application kernel, and
`MediatorCommandDispatcher` / `MediatorQueryDispatcher` adapt them — so the mediator is an
implementation detail of the shared infrastructure rather than a dependency of every handler.

Two registration constraints bind any host, both discovered the hard way and recorded in
[`outbox.md`](../outbox.md) §7:

- **The mediator must be registered scoped.** The default singleton resolves every handler from the
  root scope, which works by accident while everything shares one scope and breaks the outbox drain's
  one-scope-per-message save — the handler would write to a context nobody saves.
- **The pipeline is declared inline, in the same call**, as the mediator's own ordered array. The
  generator parses that syntax to emit one closed registration per message and behavior; a second
  `AddMediator` with a different array leaves it to pick a winner.

The order is the guarantee: `LoggingBehavior` outermost so refusals still leave a line, then
`ValidationBehavior`, then `UnitOfWorkBehavior` so a malformed command never writes.

## Consequences

Dispatch is compile-time. A command with no handler, or two, is a build error rather than a runtime
one, and the generated registrations honor each behavior's generic constraints — which is what lets
`UnitOfWorkBehavior` apply to commands and not to queries without a runtime check.

No reflection on the hot path, and the pipeline is inspectable: the generated code is what runs.

The cost is that the registration syntax is load-bearing. Moving the behavior array into a variable
or a helper method compiles and silently produces a different pipeline, which is exactly the class of
failure this repository writes documents about — hence the constraint above being stated in
[`outbox.md`](../outbox.md) rather than left to a comment.

**One deliberate exception to the abstraction.** `MediatorDomainEventPublisher` is the only file in
the solution that names a mediator directly, and it uses the `object` overload on purpose, so
handlers resolve on the event's runtime type rather than on the interface the variable happens to
have. Replacing the delivery mechanism with a bus is one registration.

## Alternatives rejected

**MediatR.** Runtime reflection, a licensing change under way in the ecosystem, and — the reason that
would have mattered anyway — handler resolution failures that surface as a runtime exception on the
first request rather than as a build error.

**No mediator at all**, injecting handlers directly. It removes a dependency and removes the pipeline
with it: logging, validation and the unit of work would each become something every handler
remembers to do.
