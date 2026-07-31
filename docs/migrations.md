# Migrations, and why there are none yet

How a schema comes into being, why that mechanism is not the one a deployed system will use, and
what has to happen the day it changes. Like [outbox.md](outbox.md), this document records a
technical choice and its reasons, so that revisiting it is a decision rather than an accident.

## 1. What builds a schema today

`EnsureCreatedAsync`, in the two `SqlServerFixture` classes and in the composition tests. Nowhere
else — the modules describe tables and indexes, and nothing in `src/` ever creates one.

For a fixture that drops its database and rebuilds it, that is the right tool and not a compromise:
a test wants the model **as it is now**, not a replay of how it got there. Replaying a hundred
migrations to assert on the current shape would be slower and would prove something nobody asked
about.

It is worth being plain about the cost anyway, because it is the one that eventually forces the
change: **the tests validate a schema built by a different mechanism than a deployment would use.**
A migration that drifted from the model would pass every test in this repository.

## 2. Why there are no migrations

Two reasons. The first is temporary and the second is structural.

**Nothing is deployed.** There is no host, no database holding data, no environment whose schema has
to survive a change. A migration is a deployment artifact, and generating them now would mean
carrying them through the whole of Circulation, Members and Charges — every rename, every column,
every index — for a schema nobody runs. The ubiquitous-language pass that renamed `PersonName`,
`AuthorizedName` and `Work.Title` would have shipped with a migration each, describing the evolution
of a database that never existed.

**A migration names a provider, and a module may not.** This is the reason that outlives the first
one. Every infrastructure project references `Microsoft.EntityFrameworkCore.Relational` and no
provider, deliberately, and says so where it does it:

> *Relational, not a provider. The module's mapping is tables and schemas; which database serves
> them is the host's decision, made once at startup.*

`Microsoft.EntityFrameworkCore.SqlServer` is declared once, in the `Testing` group of
`Directory.Packages.props`. A migration is provider-specific down to its generated code —
`nvarchar(max)`, `rowversion`, `IDENTITY` — so putting one inside `Catalog.Infrastructure` would put
SQL Server inside a module that has a written rule against naming an engine.

**A host is not required to generate them, and that is not the obstacle.** The tooling prefers a
startup project because discovering an `IHostBuilder` is convenient, but
`IDesignTimeDbContextFactory<TContext>` exists precisely for a context the tooling cannot construct
on its own, and `migrations add` never connects to a database. What is missing is not a host. It is
somewhere legitimate to put the provider.

## 3. The shape they will take

Recorded now so the day it happens is an execution rather than an investigation.

**One migrations project per module** — `LibraryManagement.Catalog.Migrations.SqlServer` and its
Holdings counterpart — each referencing its module and the provider, holding an
`IDesignTimeDbContextFactory<T>` and a `Migrations/` folder, with `MigrationsAssembly()` pointing the
runtime at it. It is the only arrangement that leaves the modules agnostic, and it puts the choice
of engine exactly where the strategic design already puts it: beside the host, not inside a module.

**Two contexts on one database need two history tables.**

```csharp
options.UseSqlServer(connectionString, sql => sql
    .MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbContext.Schema));
```

Left to the default, both modules share `dbo.__EFMigrationsHistory`, and each then reads the other's
rows as migrations of its own that it has never applied — or worse, as ones it should revert. It is
the same principle as a schema per module, one level down, and it fails in a way that looks like
corruption rather than like a configuration mistake.

**What the switch removes.** `TwoModulesTests` reaches for the relational creator's
`CreateTablesAsync` because `EnsureCreated` creates the *database* and then answers "already there"
for the second context over it, leaving that module's tables unbuilt. With migrations, both apply. The
fixtures move to `MigrateAsync` at the same time, and the migrations start being validated by the
integration suite instead of bypassed by it.

## 4. When

**The first host, or the first database that holds data — whichever comes first.**

Written down because a deferral with no trigger is not a decision but a habit. Until one of those two
things exists, `EnsureCreated` costs a three-line workaround in one test; after either of them, it
costs a schema nobody can change.

One rule already assumes this document's subject exists: [outbox.md](outbox.md) §3 requires that
renaming a stored event type either drain the table first **or ship with a migration rewriting the
stored names**. Today only the first half is available. That is survivable exactly as long as §2's
first reason holds.
