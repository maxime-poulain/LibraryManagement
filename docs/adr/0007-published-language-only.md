# ADR-0007 — Modules speak only through published languages of primitives

- **Status**: Accepted
- **Date**: 2026-08-05
- **Authority**: [`outbox.md`](../outbox.md) §9, [`strategic-design.md`](../strategic-design.md) §8

## Context

[ADR-0002](0002-one-dbcontext-and-schema-per-module.md) separates the stores. Something still has to
carry a fact from Circulation to Charges, and a question from Circulation to Holdings. The obvious
route — let the consumer subscribe to the producer's domain event — exports the model that raised it:
the consumer compiles against `LoanId`, `CopyId` and whatever those drag along, and every refactoring
of the publisher's domain becomes a change to somebody else's code.

## Decision

Each module owns a `*.PublishedLanguage` project, and it is **the only thing another module may
reference**. No module references another's `Domain`, `Application` or `Infrastructure`.

A published language **references nothing** — not the shared kernel, not the mediator — and traffics
in primitives: `Guid`, `decimal`, `int`, `string`. It carries two kinds of thing:

- **Flat contracts**: `record`s of primitives announcing a fact. `CopyReportedLost`,
  `LoanReturned`, `LoanEndedUnreturned`, `MemberBalanceChanged`.
- **Ports**: interfaces for the synchronous desk-time questions. `IEditionCatalog`, `IMemberBalance`.

One domain event is flattened into **as many contracts as it has audiences**. `LoanDeclaredLost`
leaves Circulation twice — as `CopyReportedLost` carrying the copy and nothing else, and as
`LoanEndedUnreturned` carrying the borrower too — because Holdings has no use for a borrower and a
contract that offered one would invite it to grow a use.

**The publisher translates.** A handler in the publishing module's infrastructure receives its own
domain event and publishes the flat contract. It cannot be the other way round: the consumer may not
reference the publisher's `Domain`, so it could not name the event to subscribe to it.

## Consequences

That the published language references nothing is **load-bearing, not incidental**. It means a
contract cannot implement a marker interface from the shared kernel — not `IDomainEvent`, not the
mediator's `INotification`. So the mediator cannot carry this hop, and subscribers are resolved from
the container by the contract's own type. The mechanism follows from the constraint.

A module's domain can be refactored freely. Nothing outside it compiles against its types.

The cost is duplication that looks like waste and is not: Holdings redeclares `EditionId`, Charges
redeclares `MemberId` and `CopyId`, each carrying the same `Guid` as the issuer's. The model is
translated; the identity never is. Nobody should go looking for a correspondence table.

Consumer-side documentation names the **contract**, never the publisher's domain event — the tactical
designs' "Consumed" tables say `CopyReportedLost` and `LoanEndedUnreturned`, because the event behind
them is a type the consumer may not name.

An architecture rule (`IntegrationContractRules`) refuses a subscriber that names anything but a
published language.

## Alternatives rejected

**Share the domain events.** One `ProjectReference` and the boundary is gone, with the consumer
recompiling on every change to the producer's model.

**A shared contracts assembly** every module references. It becomes the place concepts go to be
shared, which is a shared kernel in the DDD sense — the thing
[`strategic-design.md`](../strategic-design.md) §8 records this solution as deliberately not having.
