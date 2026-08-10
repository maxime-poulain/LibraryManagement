# Migrations

How a schema comes into being, why it took until now, and what the arrangement costs. Like
[outbox.md](outbox.md), this document records a technical choice and its reasons, so that revisiting
it is a decision rather than an accident.

## 1. What builds a schema

**The migrations**, one set per module, held in a project of its own beside the host. The host
applies all five at startup; every integration fixture drops its database and applies them too. So
the suite now validates the schema a deployment would get, which is the whole point of the switch.

It was `EnsureCreatedAsync` until the first host, and the reason that was survivable is worth
keeping: a fixture that drops its database and rebuilds it wants the model **as it is now**, not a
replay of how it got there. What made it stop being survivable is the cost this document named from
the start — **the tests validated a schema built by a different mechanism than a deployment would
use**, so a migration that had drifted from the model would have passed every test in this
repository. `MigrationsMatchTheModelTests` now asks the remaining half of that question directly, per
module: a model changed without a migration applies cleanly and is simply missing a column, and only
`HasPendingModelChanges` catches it.

## 2. Why they took until now

Two reasons. The first was temporary and the second is structural, and the structural one is what
shaped where they live.

**Nothing was deployed.** No host, no database holding data, no environment whose schema had to
survive a change. Generating them earlier would have meant carrying a migration through the whole of
Circulation, Members and Charges — every rename, every column, every index — for a schema nobody
ran. The ubiquitous-language pass that renamed `PersonName`, `AuthorizedName` and `Work.Title` would
have shipped with a migration each, describing the evolution of a database that never existed.

**A migration names a provider, and a module may not.** This is the reason that outlives the first
one. Every infrastructure project references `Microsoft.EntityFrameworkCore.Relational` and no
provider, deliberately, and says so where it does it:

> *Relational, not a provider. The module's mapping is tables and schemas; which database serves
> them is the host's decision, made once at startup.*

A migration is provider-specific down to its generated code — `nvarchar(max)`, `rowversion`,
`IDENTITY` — so putting one inside `Catalog.Infrastructure` would put SQL Server inside a module
that has a written rule against naming an engine.

**A host was never required to generate them, and was never the obstacle.** The tooling prefers a
startup project because discovering an `IHostBuilder` is convenient, but
`IDesignTimeDbContextFactory<TContext>` exists precisely for a context the tooling cannot construct
on its own, and `migrations add` never connects to a database. What was missing was not a host. It
was somewhere legitimate to put the provider — and building the host is what created that place.

## 3. The shape they have

**One migrations project per module**, under `src/Host/`:
`LibraryManagement.Catalog.Migrations.SqlServer` and its four counterparts. Each references its
module and the provider, and holds three things: a `Migrations/` folder, an
`IDesignTimeDbContextFactory<T>` so the tooling can build the context without a host, and the one
extension that spells the provider call.

**The provider call is spelled once, in a sixth project the five reference.**
`LibraryManagement.Migrations.SqlServer` holds it, and nothing else does:

```csharp
public static DbContextOptionsBuilder UseModuleSqlServer(
    this DbContextOptionsBuilder options, string connectionString, string schema, Assembly migrations)
    => options.UseSqlServer(connectionString, sql => sql
        .MigrationsAssembly(migrations.GetName().Name)
        .MigrationsHistoryTable("__EFMigrationsHistory", schema));
```

Each module wraps it in a name of its own — `UseCatalogSqlServer` is one expression — because the
name is what callers say and the module is what they mean. The host says it at startup, each fixture
says it before every run, and the design-time factory says it for the tooling. Callers spelling
`UseSqlServer` themselves would each be a chance to forget the history table, which fails silently;
the extension exists to make forgetting impossible rather than to save typing.

**That argument is why there is a sixth project at all.** The first version wrote the whole call in
each of the five, which made five chances to forget rather than none — this document said one thing
and the code did another, and the duplication gate is what surfaced it. The rule now lives where the
sentence above claims it lives.

Both extensions come in the pair EF Core's own provider extensions come in, typed and untyped: a
host configures through the untyped builder `AddDbContext` hands it, while a fixture and the factory
build their own typed one and untyped options do not fit a context's constructor. The design-time
factories share a base for the same reason, and each module's is three lines: which extension to
call, and how to construct its context.

**Five contexts on one database need five history tables.** Left to the default they share
`dbo.__EFMigrationsHistory`, and each then reads the others' rows as migrations of its own that it
has never applied — or worse, as ones it should revert. It is the same principle as a schema per
module, one level down, and it fails in a way that looks like corruption rather than like a
configuration mistake.

**What the switch removed.** `TwoModulesTests` reached for the relational creator's
`CreateTablesAsync` because `EnsureCreated` creates the *database* and then answers "already there"
for the second context over it, leaving that module's tables unbuilt; three other composition tests
had copied the workaround. With a history table per schema, every context applies its own, and all
four call `MigrateAsync` like everyone else.

**Adding one.** From the repository root, with the local tools restored:

```bash
dotnet ef migrations add <Name> \
  --project src/Host/LibraryManagement.Catalog.Migrations.SqlServer \
  --startup-project src/Host/LibraryManagement.Catalog.Migrations.SqlServer
```

The migrations project is its own startup project: the factory is there, and nothing has to boot a
host to write a migration. The generated files are marked as generated code in `.editorconfig` —
they are the tooling's output, rewritten whenever the model moves, and reformatting them by hand
would be undone by the next `migrations add`.

## 4. When it happened

**At the first host**, which is what [ADR-0010](adr/0010-ensurecreated-before-migrations.md) named as
its trigger — that, or the first database holding data, whichever came first. Written down at the
time because a deferral with no trigger is not a decision but a habit; honored here because a
trigger nobody acts on is the same thing.

One rule had been waiting on this: [outbox.md](outbox.md) §3 requires that renaming a stored event
type either drain the table first **or ship with a migration rewriting the stored names**. Both
halves exist now, which is what makes the outbox tables safe to rename against once they hold rows.
