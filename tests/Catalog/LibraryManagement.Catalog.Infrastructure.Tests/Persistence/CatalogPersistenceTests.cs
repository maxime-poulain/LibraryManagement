using LibraryManagement.Catalog.Application.Works.GetWorkById;
using LibraryManagement.Catalog.Domain;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
public sealed class CatalogPersistenceTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PersonName Name(string value)
        => PersonName.Create(value).Match(name => name, _ => throw new InvalidOperationException());

    private static Title TitleOf(string value)
        => Title.Create(value).Match(title => title, _ => throw new InvalidOperationException());

    private static LifeYears Years(int? birth, int? death)
        => LifeYears.Create(birth, death).Match(years => years, _ => throw new InvalidOperationException());

    private static Author AnAuthor(string name = "Saint-Exupéry, Antoine de")
        => Author.Register(
            AuthorId.Generate(),
            Name(name),
            LifeYears.Create(1900, 1944).Match(years => years, _ => LifeYears.Unknown));

    private static Work AWork(Title title, params AuthorId[] authors)
        => Work.Register(WorkId.Generate(), title, authors)
            .Match(work => work, errors => throw new InvalidOperationException(errors[0].ToString()));

    // --- The unit of work is what writes ------------------------------------------------------------

    [Fact]
    public async Task SavingTheUnitOfWork_WritesEverythingTheCommandChanged()
    {
        // No handler and no repository calls SaveChanges. The unit of work does, once, when the
        // pipeline has seen the command report success — which is how "the command succeeded" and
        // "the work was written" become one event.
        await using var context = sqlServer.NewContext();
        var author = AnAuthor();
        new AuthorRepository(context).Add(author);

        await new CatalogUnitOfWork(context).SaveChangesAsync(Token);

        await using var reader = sqlServer.NewContext();
        (await new AuthorRepository(reader).GetByIdAsync(author.Id, Token)).ShouldNotBeNull();
    }

    [Fact]
    public async Task NotSavingTheUnitOfWork_WritesNothing()
    {
        // No rollback is involved, and none is needed: repositories only track, so a command that
        // reports a failure simply never reaches the save and leaves the store untouched. That is
        // why there is no explicit transaction to open or discard.
        await using var context = sqlServer.NewContext();
        var author = AnAuthor();

        new AuthorRepository(context).Add(author);

        await using var reader = sqlServer.NewContext();
        (await new AuthorRepository(reader).GetByIdAsync(author.Id, Token)).ShouldBeNull();
    }

    // --- The write side round-trips the domain, not a shadow of it --------------------------------

    [Fact]
    public async Task AnAuthor_ComesBackWithItsHeadingAndItsYears()
    {
        var author = AnAuthor();
        await SaveAsync(context => new AuthorRepository(context).Add(author));

        await using var reader = sqlServer.NewContext();
        var found = await new AuthorRepository(reader).GetByIdAsync(author.Id, Token);

        found.ShouldNotBeNull();
        found.AuthorizedName.ShouldBe(Name("Saint-Exupéry, Antoine de"));
        found.LifeYears.Birth.ShouldBe(1900);
        found.LifeYears.Death.ShouldBe(1944);
    }

    [Fact]
    public async Task AnAuthor_ComesBackWithEveryVariantName()
    {
        // A rename reads and rewrites the variants, so loading half an author would let a change be
        // made against a state that was never true.
        var author = AnAuthor();
        author.AddVariantName(Name("Saint Exupery, Antoine de"));
        author.Rename(Name("Saint-Exupéry, A. de"));

        await SaveAsync(context => new AuthorRepository(context).Add(author));

        await using var reader = sqlServer.NewContext();
        var found = await new AuthorRepository(reader).GetByIdAsync(author.Id, Token);

        found.ShouldNotBeNull();
        found.AuthorizedName.ShouldBe(Name("Saint-Exupéry, A. de"));
        found.VariantNames.ShouldBe(
            [Name("Saint Exupery, Antoine de"), Name("Saint-Exupéry, Antoine de")],
            ignoreOrder: true);
    }

    [Fact]
    public async Task ExistsAsync_AnswersWithoutLoadingTheRecord()
    {
        var author = AnAuthor();
        await SaveAsync(context => new AuthorRepository(context).Add(author));

        await using var reader = sqlServer.NewContext();
        var authors = new AuthorRepository(reader);

        (await authors.ExistsAsync(author.Id, Token)).ShouldBeTrue();
        (await authors.ExistsAsync(AuthorId.Generate(), Token)).ShouldBeFalse();
        reader.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task AWork_ComesBackWithTheAuthorsItCredits()
    {
        var first = AnAuthor("Deleuze, Gilles");
        var second = AnAuthor("Guattari, Félix");
        var work = AWork(TitleOf("Mille plateaux"), first.Id, second.Id);

        await SaveAsync(context =>
        {
            new AuthorRepository(context).Add(first);
            new AuthorRepository(context).Add(second);
            new WorkRepository(context).Add(work);
        });

        await using var reader = sqlServer.NewContext();
        var found = await new WorkRepository(reader).GetByIdAsync(work.Id, Token);

        found.ShouldNotBeNull();
        found.Title.ShouldBe(TitleOf("Mille plateaux"));
        found.AuthorIds.ShouldBe([first.Id, second.Id], ignoreOrder: true);
    }

    // --- Optimistic concurrency, end to end --------------------------------------------------------

    [Fact]
    public async Task TwoEmployeesChangingTheSameRecord_TheSecondIsRefused()
    {
        // The case the command dispatcher translates into a failed Result. The token is a SQL Server
        // rowversion the engine moves on every write, so nothing in the application can forget to.
        //
        // Both employees correct the dates rather than the name: a rename also files the outgoing
        // heading as a variant, and two of them would collide on that table's key first — a real
        // failure, but a different one, and it would hide the token doing its work.
        var author = AnAuthor();
        await SaveAsync(context => new AuthorRepository(context).Add(author));

        await using var first = sqlServer.NewContext();
        await using var second = sqlServer.NewContext();

        var readByFirst = await new AuthorRepository(first).GetByIdAsync(author.Id, Token);
        var readBySecond = await new AuthorRepository(second).GetByIdAsync(author.Id, Token);

        readByFirst!.CorrectLifeYears(Years(1900, 1945));
        await first.SaveChangesAsync(Token);

        readBySecond!.CorrectLifeYears(Years(1901, 1944));

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            async () => await second.SaveChangesAsync(Token));
    }

    // --- The read side answers without loading an aggregate ---------------------------------------

    [Fact]
    public async Task TheQueryHandler_ProjectsAWorkAndTheNamesOfItsAuthors()
    {
        var author = AnAuthor("Deleuze, Gilles");
        var work = AWork(TitleOf("Différence et répétition"), author.Id);

        await SaveAsync(context =>
        {
            new AuthorRepository(context).Add(author);
            new WorkRepository(context).Add(work);
        });

        await using var reader = sqlServer.NewContext();
        var result = await new GetWorkByIdQueryHandler(reader)
            .Handle(new GetWorkByIdQuery(work.Id.Value), Token);

        var details = result.Match(found => found, errors => throw new InvalidOperationException(errors[0].ToString()));

        details.Title.ShouldBe("Différence et répétition");
        details.Authors.Single().AuthorizedName.ShouldBe("Deleuze, Gilles");
        // Nothing was materialized, so nothing can be changed through it by accident.
        reader.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task TheQueryHandler_FailsForAWorkThatIsNotCatalogued()
    {
        // A work that is not catalogued is a failure and not an empty answer: the caller asked for
        // one particular work, and a null would leave them to tell absence from breakage.
        await using var reader = sqlServer.NewContext();

        var result = await new GetWorkByIdQueryHandler(reader)
            .Handle(new GetWorkByIdQuery(Guid.CreateVersion7()), Token);

        result.Match<ErrorCode?>(_ => null, errors => errors[0].ErrorCode)
            .ShouldBe(CatalogErrorCodes.WorkNotFound);
    }

    [Fact]
    public async Task TheQueryHandler_DoesNotSecondGuessTheValidator()
    {
        // The empty identifier never reaches here: GetWorkByIdQueryValidator rejects it at dispatch,
        // and a work that is genuinely not catalogued stays a different answer from a request that
        // was never well formed. Reaching the handler with one is a wiring mistake, and WorkId says
        // so by throwing rather than by inventing a not-found.
        await using var reader = sqlServer.NewContext();

        await Should.ThrowAsync<ArgumentException>(
            async () => await new GetWorkByIdQueryHandler(reader)
                .Handle(new GetWorkByIdQuery(Guid.Empty), Token));
    }

    private async Task SaveAsync(Action<CatalogDbContext> work)
    {
        await using var context = sqlServer.NewContext();
        work(context);

        await new CatalogUnitOfWork(context).SaveChangesAsync(Token);
    }
}
