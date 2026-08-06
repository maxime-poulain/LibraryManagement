# ADR-0001 — A modular monolith, not services

- **Status**: Accepted
- **Date**: 2026-07-27
- **Authority**: [`strategic-design.md`](../strategic-design.md) §8

## Context

The domain divides into five bounded contexts with genuinely different rates of change, vocabularies
and actors. That division is real, and it is decided in the strategic design on business grounds
rather than deployment ones. What remained was whether the division should also be a deployment
boundary.

The context map settles it by accident rather than by preference. Two of Circulation's dependencies
are **synchronous, desk-time questions**: *may this copy be lent?* to Holdings, and *how much does
this member owe?* to Charges. Both sit on the most frequent write path in the system — a checkout,
with a member standing at the counter — and both are asked on every act.

## Decision

One process, one database, five modules with hard internal boundaries. The modules are separated by
everything except deployment: their own domain model, application layer, infrastructure, `DbContext`
and schema, and a compiler-enforced rule that none may reference another's internals.

## Consequences

Extracting a module later is a real project, but a bounded one: the boundaries that make it possible
are already enforced, and what would change is the transport, not the model. The concentration of
synchronous edges on the core's write path is the documented reason to expect that extraction to be
expensive for **Circulation specifically** — Catalog or Members would leave far more cheaply.

In exchange, every cross-module call is an in-process method call: no network hop inside a checkout,
no partial failure to model, no distributed transaction to avoid, and one `SaveChangesAsync` that is
already atomic.

The boundaries have to be held by something other than the network, which is what
[ADR-0011](0011-architecture-rules-are-tests.md) exists for. This is the load-bearing cost: in a
service topology, a boundary violation fails to compile because the other module is not there. Here
it compiles, and only a test refuses it.

## Alternatives rejected

**Services from the start.** It would put a network hop inside the most frequent operation of the
day, and buy independent scaling that a single public library's load does not need. Every failure
mode it introduces — retries, partial writes, eventual consistency at the desk — is one the business
would notice, in exchange for none it feels.

**A single model with five namespaces.** The cheapest thing to build and the thing this repository
exists not to be. Without separate contexts a shared `Book` between Catalog and Circulation is the
natural next step, and the vocabulary collapses to whichever module was written first.
