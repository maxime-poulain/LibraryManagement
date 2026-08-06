# ADR-0011 — Architectural rules are executable tests

- **Status**: Accepted
- **Date**: 2026-07-27
- **Authority**: `tests/Architecture/LibraryManagement.Architecture.Tests/`

## Context

[ADR-0001](0001-modular-monolith-over-services.md) records the load-bearing cost of a monolith: in a
service topology a boundary violation fails to compile because the other module is not there; here it
compiles. Project references hold the coarse boundaries, but several rules this solution depends on
are invisible to the compiler, and each one fails **silently**:

- An aggregate mapped without `AggregateRootConfiguration<,>` loses its `rowversion` concurrency
  token, and nothing reports it until two writers collide in production.
- A command handler injecting `ICommandDispatcher` turns one use case into two, with the unit of
  work keyed on an assembly that is now ambiguous.
- A subscriber reaching into another module's `Domain` ends the boundary
  ([ADR-0007](0007-published-language-only.md)) without anything failing.
- A query answering with a domain type leaks the model through the read side.

## Decision

The rules are reflection tests over the **built assemblies**, in a project of their own. Each rule is
a class (`MappingRules`, `CommandHandlerRules`, `IntegrationContractRules`, `QueryContractRules`) and
each rule has tests of its own — not only tests that the codebase obeys it.

Three properties make them trustworthy, and each is itself tested:

- **The scan finds what it is supposed to inspect.** `TheScan_FindsTheHandlersTheModulesDeclare` and
  its siblings fail if the scan reaches nothing. Green over an empty set reads exactly like green
  over everything, which is the failure mode an architecture test is most prone to.
- **The rule catches a real violation.** A deliberately non-compliant type exists for each rule, in
  an assembly the scan excludes, so `TheRule_Catches…` proves the rule can fail.
- **The rule explains itself.** `TheRule_ExplainsWhyRatherThanJustNamingTheType` — a failure that
  names a type without naming the rule sends the reader to `git blame`.

## Consequences

The rules run in CI's required check, on every pull request, with no Docker needed.

**Adding a module means wiring it into this project explicitly**: a `ProjectReference` *and* one
pinned handler in `CommandHandlerRulesTests`. The scan only sees referenced assemblies, so a module
nobody referenced passes every rule by being invisible — which is why the pinned handler exists, and
why `TheScan_ReachesEveryModuleAndNotOnlyTheKernel` is a test.

Some rules are held elsewhere, where a better mechanism exists, and the division is deliberate: a
unique index holds barcode uniqueness because a `Copy` cannot see the other copies; a model test in
each module's `*DbContextTests` holds `ValueGeneratedNever()` on owned keys, because that is a fact
about a model rather than about a type.

## Alternatives rejected

**Code review.** It works until the reviewer is busy, and it does not run on a branch nobody read.

**A dependency-analysis tool in CI.** It would catch the reference rules and none of the rest — the
concurrency token, the `Dto` suffix and the nested-dispatch rule are not dependency facts.

**Roslyn analyzers.** Better ergonomics, since violations would surface in the editor, at the cost of
a separate build, a distribution story and a much steeper authoring cost per rule. Worth revisiting if
the rule count grows well past the current four.
