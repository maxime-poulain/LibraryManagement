# ADR-0012 — American English, and the glossary is binding

- **Status**: Accepted
- **Date**: 2026-07-31
- **Authority**: [`strategic-design.md`](../strategic-design.md) §4

## Context

The code is written in English by standing convention; the domain is described in French by the
librarians who own it. A single language has to win, or every concept acquires two names and they
drift.

Naming a language without naming its **variety** leaves the question open at every file, and it was
answered twice in this repository before it was decided once: the schema, the error codes and the
assemblies were spelled `Catalog`, and the queries and the prose `Catalogue`.

The profession also supplies genuine synonyms — *heading* and *preferred name* are one concept in two
registers, and left alone they become two types.

## Decision

**American spelling throughout** — `Catalog`, never `Catalogue`, including in prose, comments and
document titles. The immovable half won: a schema name and an error code are contracts, a document is
not.

**§4 of the strategic design is the binding glossary.** The French term is what a librarian actually
says and is the authority when the two disagree; the English term is what the code says, once.

The most-violated edges, stated so a reviewer can check them without reading the whole table:
`PreferredName` and `VariantName` (never `Heading` or `AuthorizedName`); `PreferredTitle`;
`NameForm`; `Copy` (never `Item`); `Shelfmark` (never *call number*); `InService` (never `OnShelf`);
`Balance` in Charges but `Debt` and `Standing` in Circulation.

Error codes read `Module.PascalCase` and live in the module's `*ErrorCodes` class.

## Consequences

**One concept, one word, everywhere** — and where two contexts name the same figure differently, that
is itself a decision the glossary records rather than a drift it tolerates. `Balance` and `Debt` are
one amount under two names *on purpose*, because each context names what it does with it: Charges
records it, Circulation is what it forbids. The glossary is what keeps that distinguishable from an
accident.

**A glossary change is a code change**, and the reverse. When a term moves, the change ships with a
lexical sweep proving the abandoned form is gone everywhere except lines that name it *as* abandoned
— and those lines are kept deliberately, because a rejected name that vanished is one somebody
reintroduces.

Several glossary rows exist to hold a rename that has not happened yet: `Author` records that it
becomes an `Agent` with a role the day a translator is modeled, and `EditionStatement` records that
the profession says *édition* for two things the model may only say it for one. Naming the future
rename makes it a decision rather than a discovery.

**The cost is real and is accepted**: renaming a stored event type or an event `record`'s positional
parameter breaks every stored outbox payload ([ADR-0006](0006-transactional-outbox-per-module.md)).
Language passes are therefore cheap now, while the tables are empty, and expensive after the first
deployment — which is one more thing
[ADR-0010](0010-ensurecreated-before-migrations.md)'s trigger is measuring.

## Alternatives rejected

**French in the code.** It matches the domain experts exactly and costs every library, framework and
future contributor a translation at the boundary.

**English without naming the variety.** Tried by omission, and it produced two spellings of the
central noun in one codebase.

**A translation table instead of a chosen term.** Two names per concept, maintained forever, drifting
the first time somebody adds a row to only one side.
