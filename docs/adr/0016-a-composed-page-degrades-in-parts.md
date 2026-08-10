# ADR-0016 — A composed page degrades in parts, and names what is missing

- **Status**: Accepted
- **Date**: 2026-08-10
- **Authority**: [`strategic-design.md`](../strategic-design.md) §10

## Context

§10 decided how a page of *detail* is read: a member's file starts from an identifier already in
hand, so the host dispatches each module's published query and assembles a view model of its own —
Members for who the person is, Circulation for the loans, the holds and the standing, Charges for
the balance. It also decided the queries answer with a `Result`, and said why: *so the page chooses
its own degradation when one module cannot answer, which no join would have offered.*

It stopped there, correctly. What the page then does with a refusal is not a question about
boundaries, and it could not be answered before a page existed. One does now, and it has to answer
it — three sequential reads against three schemas will one day return two answers and a failure.

## Decision

**A member's file answers 200 with the parts it has, and names the parts it does not.** The
response carries `member`, `circulation` and `charges`; the last two are `null` when their module
refused, and an `unavailable` array names each missing part with the error code and message that
module gave.

**Members is the exception, and it is the only one.** A file is a file *of* somebody. If Members
cannot answer, there is nothing to arrange the rest around, so that refusal travels as the caller's
— 404 when nobody is enrolled under the identifier, by the mapping ADR-0015 already fixed. The
other two are answered *about* a person who exists, and their absence leaves a page that is still
worth showing.

**A blank is never a zero.** The reason the missing parts are named rather than merely absent: a
balance panel that renders empty is indistinguishable from a member who owes nothing, and the
difference decides whether a librarian lets someone borrow. Naming it costs one array and removes
the only reading that could do harm.

## Consequences

The desk keeps working through a partial outage of the system, which is the point: a member can be
identified and their loans seen while Charges is down, and the screen says the money is unknown
rather than implying it is nothing.

**The composer still decides nothing about the domain.** It arranges answers and reports absences;
it does not fill a gap with a default, retry, or infer. In particular it never derives `Standing`
from the balance it happens to be holding — that judgement is Circulation's, and a composer that
formed its own would put a rule where no module's invariants cover it. The unit tests around
`ComposeAsync` hold exactly this: one hands the composer a blocked-standing verdict alongside a
contradicting balance and asserts the page repeats what it was told.

The status code says the request succeeded, because it did — the resource was found and returned.
A 207 would describe the composition rather than the outcome, and no browser or client library
treats it as success. What is partial is inside the body, where the client can see it.

`unavailable` carries the **first** error of each refusing module, where a problem document carries
every one. Different readers: a problem document goes to a caller who must correct something, and
hiding faults makes them iterate; this is a note saying which panel is blank, and a librarian who
cannot act on one reason cannot act on four.

## Alternatives rejected

**All or nothing — any refusal fails the whole page.** The simplest rule, and the one that turns a
Charges outage into a desk that cannot identify a member. It also throws away the answers that did
arrive, which the sequential composition has already paid for.

**Absent rather than named.** Omitting the failed parts and saying nothing keeps the response
smaller and makes an outage look like an empty account. This is the reading that costs money and
trust, and it is the reason the array exists.

**A default in place of the missing part** — zero balance, empty loan list. Worse than either: it
is indistinguishable from a true answer, and it is the composer inventing domain facts, which is
precisely what §10 forbids it to do.

**207 Multi-Status.** Describes the composition rather than the outcome. Clients treat it as
unexpected, and the partiality is already in the body where a client can act on it.

**Fanning the three queries out in parallel and degrading on timeout.** Tempting and currently
impossible: the modules' `DbContext` instances are scoped and not thread-safe, so a page that fanned
out would share one across threads. The day sequential reads are measurably too slow, the answer is
the read model §7 already names, not a faster composer.
