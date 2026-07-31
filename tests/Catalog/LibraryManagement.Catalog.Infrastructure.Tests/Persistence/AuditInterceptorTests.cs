using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Shared.Application;
using LibraryManagement.Shared.Infrastructure.Auditing;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Persistence;

/// <summary>
/// The audit columns, written by the store and read back out of it.
/// </summary>
/// <remarks>
/// Against the real engine, because the claim being tested is that four columns exist and hold what
/// was put in them. Entity Framework does not discover these properties on its own — they have no
/// setter — so a test against a fake would prove the interceptor assigns a value in memory while the
/// database had no column to put it in.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public sealed class AuditInterceptorTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Two instants far enough apart to tell one from the other at a glance, and both carrying an
    // offset: an audit trail read across two sites cannot afford "half past nine" without saying
    // where.
    private static readonly DateTimeOffset Opened = new(2026, 3, 14, 9, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Corrected = new(2026, 5, 2, 16, 45, 0, TimeSpan.Zero);

    private static NameForm Name(string value)
        => NameForm.Create(value).Match(name => name, _ => throw new InvalidOperationException());

    private static Author AnAuthor(string name = "Ernaux, Annie")
        => Author.Register(AuthorId.Generate(), Name(name), LifeYears.Unknown);

    private CatalogDbContext ContextAt(DateTimeOffset now, string? actingAs)
        => sqlServer.NewContext(new AuditInterceptor(new FrozenClock(now), new ActingAs(actingAs)));

    private async Task<Author> ReadBackAsync(AuthorId id)
    {
        await using var reader = sqlServer.NewContext();
        return (await new AuthorRepository(reader).GetByIdAsync(id, Token))!;
    }

    [Fact]
    public async Task Inserting_RecordsWhenAndByWhom()
    {
        var author = AnAuthor();

        await using (var context = ContextAt(Opened, "m.poulain"))
        {
            new AuthorRepository(context).Add(author);
            await new CatalogUnitOfWork(context).SaveChangesAsync(Token);
        }

        var stored = await ReadBackAsync(author.Id);

        stored.CreatedOn.ShouldBe(Opened);
        stored.CreatedBy.ShouldBe("m.poulain");
    }

    [Fact]
    public async Task Inserting_LeavesTheModificationColumnsEmpty()
    {
        // A record that has never been changed has not been changed by anyone. A creation that also
        // wrote ModifiedOn would make every row look edited and leave nothing to distinguish the
        // ones that were.
        var author = AnAuthor("Bourdieu, Pierre");

        await using (var context = ContextAt(Opened, "m.poulain"))
        {
            new AuthorRepository(context).Add(author);
            await new CatalogUnitOfWork(context).SaveChangesAsync(Token);
        }

        var stored = await ReadBackAsync(author.Id);

        stored.ModifiedOn.ShouldBeNull();
        stored.ModifiedBy.ShouldBeNull();
    }

    [Fact]
    public async Task Updating_RecordsTheChangeAndLeavesTheCreationAsItWas()
    {
        var author = AnAuthor("Perec, Georges");

        await using (var opening = ContextAt(Opened, "m.poulain"))
        {
            new AuthorRepository(opening).Add(author);
            await new CatalogUnitOfWork(opening).SaveChangesAsync(Token);
        }

        await using (var correcting = ContextAt(Corrected, "a.colleague"))
        {
            var repository = new AuthorRepository(correcting);
            var stored = await repository.GetByIdAsync(author.Id, Token);
            stored!.CorrectLifeYears(LifeYears.Create(1936, 1982).Match(years => years, _ => LifeYears.Unknown));
            await new CatalogUnitOfWork(correcting).SaveChangesAsync(Token);
        }

        var corrected = await ReadBackAsync(author.Id);

        corrected.ModifiedOn.ShouldBe(Corrected);
        corrected.ModifiedBy.ShouldBe("a.colleague");

        // The point of the two IsModified lines in the interceptor: an update carries the whole row,
        // so without them a bug anywhere upstream could rewrite the day the record was opened.
        corrected.CreatedOn.ShouldBe(Opened);
        corrected.CreatedBy.ShouldBe("m.poulain");
    }

    [Fact]
    public async Task TheInstant_IsRecordedToTheMillisecondAndNoFurther()
    {
        // The precision the column declares, seen from the outside, and the only test here that can
        // see it: every other instant is a whole second and would survive any precision at all.
        // Undeclared, the column would be a datetimeoffset(7) and this value would come back with
        // its hundred-nanosecond tail intact — detail neither the clock nor the catalog has a use
        // for, and which no aggregate can be ordered by anyway, since an identifier is only ordered
        // to the millisecond either.
        var author = AnAuthor("Sous-seconde, Sylvie");

        await using (var context = ContextAt(Opened.AddTicks(1_234_567), "m.poulain"))
        {
            new AuthorRepository(context).Add(author);
            await new CatalogUnitOfWork(context).SaveChangesAsync(Token);
        }

        var stored = await ReadBackAsync(author.Id);

        stored.CreatedOn.ShouldBe(Opened.AddMilliseconds(123));
    }

    [Fact]
    public async Task WithNobodyActing_TheColumnsSayNothingRatherThanSomethingInvented()
    {
        // Which is where things stand until Staff Access exists. The instant is still recorded — the
        // clock always knows what time it is — and only the attribution is left alone.
        var author = AnAuthor("Anonyme");

        await using (var context = ContextAt(Opened, actingAs: null))
        {
            new AuthorRepository(context).Add(author);
            await new CatalogUnitOfWork(context).SaveChangesAsync(Token);
        }

        var stored = await ReadBackAsync(author.Id);

        stored.CreatedOn.ShouldBe(Opened);
        stored.CreatedBy.ShouldBeNull();
    }

    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ActingAs(string? identifier) : ICurrentUser
    {
        public string? Identifier => identifier;
    }
}
