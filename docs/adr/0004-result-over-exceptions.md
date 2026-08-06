# ADR-0004 — `Result` for refusals, exceptions for faults

- **Status**: Accepted
- **Date**: 2026-07-27
- **Authority**: `src/Shared/LibraryManagement.Shared.Domain/Results/`

## Context

Most of what this domain does is refuse. A checkout is refused because the membership lapsed, because
the borrower owes money, because the copy is in repair, because the cap is reached, or because
somebody else is waiting for it. Every one of those is an **ordinary outcome** a librarian reads off a
screen and acts on — not a fault, and not exceptional in any sense but the English one.

## Decision

A domain operation that can be refused returns `Result` or `Result<T>`. Refusals carry an `Error` with
a code from the module's `*ErrorCodes` class, named `Module.PascalCase`.

Exceptions are kept for what they are for: a bug, a corrupt payload the domain refuses to
materialize, a store that will not answer. The dividing line is whether a librarian could have caused
it by doing something reasonable.

A refusal must say **which** refusal it is. Circulation's renewal returns `RenewalLimitReached`,
`DebtForbidsIt` or `SomeoneIsWaiting` as distinct codes, because they ask three different things of
the borrower and "we could not renew your loan" wastes a trip to the library.

## Consequences

Refusal is in the signature. A caller that ignores a `Result` is visible at the call site, where an
unthrown exception is visible nowhere.

The cross-module passage has to translate back. A subscriber that receives a refused command turns it
into an exception on purpose, because the drain's contract is that a thrown handler leaves the
message unmarked — [ADR-0008](0008-subscriber-dispatches-its-own-command.md) records why, and
`Refusal.Throw` is the one place it happens.

Error codes are part of the contract that reaches a screen, so renaming one is a change to something
somebody reads, not an internal rename.

## Alternatives rejected

**Exceptions for refusals.** It makes the ordinary path the exceptional one, costs a stack trace on
every lapsed membership, and pushes the librarian-facing distinction between three refusal reasons
into exception *types* — where the compiler stops helping, since nothing forces a catch.

**A bare `bool` or a nullable return.** It answers *no* without answering *why*, which is precisely
the requirement §7 of the Circulation design states.
