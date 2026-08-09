# ADR-0014 — The opening calendar is Circulation's; a closed day is never billed

- **Status**: Accepted
- **Date**: 2026-08-09
- **Authority**: [`tactical-design-circulation.md`](../tactical-design-circulation.md) §1,
  [`strategic-design.md`](../strategic-design.md) §3

## Context

Every duration in the model was counted in calendar days, and a library is not open every calendar
day. A due date could fall on a closed Sunday, a public holiday, or the year-end closing — and the
member was then late, and fined, for a return the library itself had made impossible. With
`BlockingDebt` at *any amount owed*, that unfair fine also cancelled every hold they had. The
injustice concentrates exactly where it is largest: every loan due across a closure comes back
"late" together, on reopening day, to a desk that can only waive fines one by one.

The product owner decided: the administrator must be able to configure the weekly closed days and
the public holidays, and both must reach the due-date arithmetic and the fine.

## Decision

**The calendar is a datum of Circulation's policy** — `OpeningCalendar`, carried by
`CirculationPolicy` beside the durations it qualifies. Weekly closed days plus dated closures;
holidays are entered as dates, never computed. The empty calendar means open every day, and at
least one weekday must stay open. Today the host configures it as it configures every policy
value; the day policy comes from a table, the calendar comes with it — no deployment either way.

Two rules consume it:

- **A deadline a member must meet never falls on a day they cannot meet it.** Due dates (checkout
  and renewal) and pickup deadlines slide to the first open day. Derived once, at the act; the
  stored date never moves afterwards.
- **A closed day is never billed.** `daysLate` counts the open days past the due date, judged
  through the calendar *as it stands* — the stored date is slid again before counting, so a
  closure declared after the act (a strike on a printed due date) is forgiven rather than fined.

Deliberately untouched: reminder stages and `DeclaredLostAfter` stay in elapsed days — a message
says how long, a fine says what it costs — and the pickup window slides without being recounted in
open days, since nothing is billed per day there.

## Consequences

**The meaning of `daysLate` crosses a boundary, and that is why this is an ADR.** The
`LoanReturned` contract now carries open days, net of closures. Charges multiplies the number
it is told and never learns the calendar — the tariff stays in the money context, the calendar
stays at the desk, and a grace period remains meaningful because the days it forgives are days the
member could actually have used. Nothing about the contract's *shape* changed, so no stored
payload breaks.

One library, one calendar, one owner. Members and Holdings never see it: a membership lapses on
the date it lapses, and a copy's status has no schedule. The day a second context needs opening
days, that is a boundary conversation — not a shared kernel.

The scheduled process keeps no desk hours: it runs on closed days, and only its expiry judgement
is calendar-aware — a claim expires once the first open day on or after its deadline has passed.

The cost is a second measure of lateness. `daysOverdue` (elapsed, in messages) and `daysLate`
(open, in fines) now genuinely differ across a closure, and the tactical design records why
folding them would make either the message lie or the fine unfair.

## Alternatives rejected

**Calendar days throughout, closures ignored.** The status quo, and the recorded injustice above.
Every library the profession runs slides its due dates; a model that does not is wrong about the
domain, not simpler about it.

**Computing the holidays.** Easter arithmetic, Alsace-Moselle's two extra days, and a set that
changes by decree — a rule wrong somewhere every year, against a list retyped once a year and
wrong nowhere.

**Naming the open days instead of the closed ones.** An unconfigured host would then run a library
that is never open, and every deadline would slide forever. The failure mode of the chosen
polarity is merely the past: an empty calendar behaves exactly as the system always had.

**Counting the pickup window in open days.** It protects against nothing — the slide already
guarantees a last day the member can walk in on — and immobilizes the copy longer for everyone
queued behind the claim.

**Charges subtracting the closures.** The desk's calendar in the money context, and the boundary
gone: an amnesty would then need to know when the library was shut.
