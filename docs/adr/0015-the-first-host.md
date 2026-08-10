# ADR-0015 — The first host

- **Status**: Accepted
- **Date**: 2026-08-09
- **Supersedes**: [ADR-0013](0013-composition-root-in-the-tests.md) — no runnable host
- **Authority**: [`outbox.md`](../outbox.md) §7 and §10, [`migrations.md`](../migrations.md),
  [`strategic-design.md`](../strategic-design.md) §10

## Context

[ADR-0013](0013-composition-root-in-the-tests.md) decided there would be no runnable host, and it
was right for as long as it held: an HTTP surface written before Circulation existed would have
shaped every module that followed. All five now exist, their moments are decided on paper and built,
and the composition tests have proven the pipeline about as far as a test can. What they cannot
prove is that anything *runs*.

That record also warned this would not be a small change: **adding a host is the trigger for several
deferred decisions at once** — migrations, the retention window that makes erasure real, the log
sinks. It was accurate. Migrations and the purge landed first, each in a change of its own; what
remains here is the host itself.

## Decision

**One ASP.NET Core process**, `src/Host/LibraryManagement.Host`, that composes the five modules,
migrates their schemas, serves the desk over HTTP, and runs the scheduled work. One process and one
database, as [ADR-0001](0001-modular-monolith-over-services.md) says.

**The composition is the one the documents already wrote.** The mediator scoped, its pipeline
declared inline in the same call in the order logging → validation → unit of work, all five
`Add<Module>Module` extensions against one connection string. None of that is a decision taken here;
it is `outbox.md` §7 and ADR-0005 being obeyed by the first thing that could obey them.

**The employee arrives in a header, and nothing verifies it.** `X-Employee-Id` fills a scoped
`ICurrentEmployee`, which is what the audit columns record. This is attribution, not authentication:
Staff Access is a generic subdomain the strategic design puts outside the model, and pretending
otherwise would be worse than saying so. A request without the header is served and audited as
nobody's, which is the truth.

**The scheduled work is registered here and nowhere else.** Five drains on `Cron.Minutely`, the
daily run on `Cron.Daily`, and the purge on `Cron.Daily`, through the `OutboxJobs`,
`CirculationDailyRun` and `OutboxPurgeJob` surfaces — which moved out of the composition tests into
this project, because that is where the documents always said they belonged.

**The retention window is thirty days**, in configuration. Long enough to answer *was this
delivered, and when?* about anything anyone still remembers, and to leave an operator a month with a
dead letter before it goes.

**The dashboard is on, for local requests only.** It can delete and re-enqueue jobs, and nothing
here authenticates anybody; an operator looking at a blocked queue is on the machine that runs the
host.

**Migrations run before Hangfire is touched.** Its SQL Server storage installs its own schema when
constructed, which the dashboard does at map time — so a database that does not exist yet would meet
Hangfire before it meets the modules. The order in `Program.cs` is load-bearing and says so.

### The HTTP surface

**Minimal APIs, one group per module, and the command record is the request body.** No request type
stands between them: a command is already a flat record of primitives that a validator refuses when
it is wrong, and a parallel type would be a second place to add a field and a first place to forget
one. Creation is a POST on the plural resource; every other act is a POST on a named route —
`/catalog/authors/rename` beside `/catalog/authors/correct-preferred-name`, because those two write
the same field and differ only in what they leave behind, which no PATCH body could say.

**Identifiers are the caller's**, as every creating command already assumed. A repeated POST then
collides on the key instead of minting a second aggregate, so a retry after a timeout is safe by
construction rather than by a deduplication table.

**Only what a librarian does is routed — 37 commands of 52.** Seven belong to the daily process.
Eight more exist because one module reacts to another: a replacement charge raised by a write-off, a
hold released because a copy left service, a lost copy noted as accounted for. Those have no desk
audience, and a route for them would let a request forge a fact only the announcing module is
entitled to state. `DeclareCopyLost` is routed despite also being a subscriber's command, because a
stocktake that fails to find a copy reaches the same fact by a different road.

**A refused command is 422; only a query can answer 404.** The split is made by the shape of the
call, never by reading the error codes — and that is the correction a test forced. The first version
answered 404 wherever every code ended in `NotFound`, which is wrong for a command: the route names
an act and exists whether or not the referent does. Worse, it cannot be made right, because
`Catalog.AuthorNotFound` means *the author being renamed* in one handler and *the author being
credited* in another, and the mapper cannot tell them apart. So the status says only what the
protocol can know, and the codes travel in the problem document for a caller that wants more. 400 is
validation, 409 is a concurrency conflict, and the body always carries every error rather than the
first.

## Consequences

**The jobs moved, and their tests with them.** `tests/Host/LibraryManagement.Host.Tests` boots the
real host through `WebApplicationFactory`, which makes *booting* an assertion: a broken composition,
a migration that will not apply, or the Hangfire ordering above fails there before any test body
runs. The daily-run test got stronger in the move — it used to answer its own cross-module questions
with stand-ins (`NoChargesYet`, `EveryEditionStillServes`), and now Holdings and Charges answer them.

**The composition tests may never reference the host.** Mediator's source generator emits a public
`Mediator` and an `AddMediator` per assembly that carries it; two such assemblies where one
references the other collide by name. So the generator lives in exactly two projects — this host and
the composition tests — and neither may reference the other. The composition tests keep everything
that does not need the host, and lost nothing but the two clockwork classes.

**A switch decides whether this process runs the jobs.** `Hangfire:RunServer`, on by default. The
storage is shared, so the day the desk's traffic and the drains want separate machines, the worker
is this same host with its web pipeline idle rather than a second program that would have to
re-derive what a day consists of. The tests use it too, because a server ticking underneath an
assertion about an outbox is a race.

**The host's internals are visible to its tests**, which is a first here and stays confined to it.
A host's public surface is HTTP, not types, so the endpoint groups and the result mapper are
internal — and a mapper tested only through requests would need a database to assert a status code.
Modules keep testing through their public seams.

**What is still absent, now for smaller reasons.** No authentication. **Almost no read side**: two
queries exist, both in Catalog, so the API is nearly write-only. That is the sharpest thing this
change makes visible — the strategic design §10 decides how a page is assembled, each module's
published query dispatched at the edge into a view model belonging to the presentation, and four
modules have nothing to dispatch. The member file is the obvious next piece of work, and it is a
Members, Circulation and Charges change before it is a host one. No `Discovery` projection. No
Dockerfile or deployment description.

## Alternatives rejected

**Keeping the composition root in the tests and adding only a worker.** ADR-0013 rejected this
already — a console host that only drains proves less than the composition tests do, since it
exercises no command path — and the argument did not change.

**Real authentication now.** Staff Access is generic and out of the modeled domain. Building it here
would decide an access model for a system that has no second client, and the header is a seam that
holds whatever replaces it.

**A separate worker process from the start.** Two deployables to keep in step, for a library whose
whole system is one process and one database. The switch above costs one line and defers the split
until something asks for it.

**Purging inside the drain.** How long history is kept and how often the queue is emptied answer to
different things — one to what the library may keep about a member, the other to a reader's latency.
Tying them would make the retention window a consequence of the tick.

**A request type per endpoint.** It would be a copy of the command with the same fields, validated
by nothing, kept in step by hand. The command is already the contract; a DTO in front of it buys
insulation from a change that would have to be made twice anyway.

**REST resources rather than named acts.** A PUT or PATCH on `/catalog/authors/{id}` would collapse
`Rename` and `CorrectPreferredName` into one request whose body cannot say which happened — and the
difference between them is the entire reason Catalog has two operations. The routes name the acts
because the model does.

**Routing every command.** It would give the desk a way to raise a fine with no loan behind it and
to cancel a hold on behalf of a debt nobody reported. The commands that exist as reactions are not a
smaller kind of desk act; they are another module's sentence, and only that module gets to say it.
