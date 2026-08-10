using LibraryManagement.Catalog.Application.Works.GetWorkById;
using LibraryManagement.Catalog.Domain;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Persistence;
using LibraryManagement.Catalog.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CatalogPersistenceTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NameForm Name(string value)
        => NameForm.Create(value).Match(name => name, _ => throw new InvalidOperationException());

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
    public async Task AnAuthor_ComesBackWithItsPreferredNameAndItsYears()
    {
        var author = AnAuthor();
        await SaveAsync(context => new AuthorRepository(context).Add(author));

        await using var reader = sqlServer.NewContext();
        var found = await new AuthorRepository(reader).GetByIdAsync(author.Id, Token);

        found.ShouldNotBeNull();
        found.PreferredName.ShouldBe(Name("Saint-Exupéry, Antoine de"));
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
        found.PreferredName.ShouldBe(Name("Saint-Exupéry, A. de"));
        found.VariantNames.ShouldBe(
            [Name("Saint Exupery, Antoine de"), Name("Saint-Exupéry, Antoine de")],
            ignoreOrder: true);
    }

    [Fact]
    public async Task RenamingAnAuthorAlreadyOnFile_FilesTheOutgoingNameAsAVariant()
    {
        // The path RenameAuthorCommandHandler actually takes: load, rename, save. The test above
        // proves the insert of a whole new author; this one proves the far commoner case, a
        // variant added to a record the store already holds.
        var author = AnAuthor();
        await SaveAsync(context => new AuthorRepository(context).Add(author));

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new AuthorRepository(updating).GetByIdAsync(author.Id, Token);
            loaded!.Rename(Name("Saint-Exupéry, A. de"));
            await new CatalogUnitOfWork(updating).SaveChangesAsync(Token);
        }

        await using var reader = sqlServer.NewContext();
        var found = await new AuthorRepository(reader).GetByIdAsync(author.Id, Token);

        found.ShouldNotBeNull();
        found.PreferredName.ShouldBe(Name("Saint-Exupéry, A. de"));
        found.VariantNames.ShouldBe([Name("Saint-Exupéry, Antoine de")]);
    }

    [Fact]
    public async Task CreditingAnAuthorToAWorkAlreadyOnFile_KeepsTheCredit()
    {
        var author = AnAuthor("Deleuze, Gilles");
        var work = AWork(TitleOf("L'Anti-Œdipe"));

        await SaveAsync(context =>
        {
            new AuthorRepository(context).Add(author);
            new WorkRepository(context).Add(work);
        });

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await new WorkRepository(updating).GetByIdAsync(work.Id, Token);
            loaded!.CreditAuthor(author.Id).HasErrors().ShouldBeFalse();
            await new CatalogUnitOfWork(updating).SaveChangesAsync(Token);
        }

        await using var reader = sqlServer.NewContext();
        var found = await new WorkRepository(reader).GetByIdAsync(work.Id, Token);

        found.ShouldNotBeNull().AuthorIds.ShouldBe([author.Id]);
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
        found.PreferredTitle.ShouldBe(TitleOf("Mille plateaux"));
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
        // preferred name as a variant, and two of them would collide on that table's key first — a real
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
        details.Authors.Single().PreferredName.ShouldBe("Deleuze, Gilles");
        // Nothing was materialized, so nothing can be changed through it by accident.
        reader.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task TheQueryHandler_FailsForAWorkThatIsNotCataloged()
    {
        // A work that is not cataloged is a failure and not an empty answer: the caller asked for
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
        // and a work that is genuinely not cataloged stays a different answer from a request that
        // was never well formed. Reaching the handler with one is a wiring mistake, and WorkId says
        // so by throwing rather than by inventing a not-found.
        await using var reader = sqlServer.NewContext();

        await Should.ThrowAsync<ArgumentException>(
            async () => await new GetWorkByIdQueryHandler(reader)
                .Handle(new GetWorkByIdQuery(Guid.Empty), Token));
    }

    // --- Editions ----------------------------------------------------------------------------------

    [Fact]
    public async Task AnEdition_ComesBackWithItsWorkAndItsIsbn()
    {
        var work = AWork(TitleOf("Le Petit Prince"));
        var edition = Edition.Register(
            EditionId.Generate(),
            work.Id,
            Isbn.Create("978-2-07-061275-8").Match(isbn => isbn, _ => throw new InvalidOperationException()));

        await SaveAsync(context =>
        {
            new WorkRepository(context).Add(work);
            new EditionRepository(context).Add(edition);
        });

        await using var reader = sqlServer.NewContext();
        var found = await new EditionRepository(reader).GetByIdAsync(edition.Id, Token);

        found.ShouldNotBeNull();
        found.WorkId.ShouldBe(work.Id);
        found.Isbn.ShouldNotBeNull().Value.ShouldBe("9782070612758");
    }

    [Fact]
    public async Task AnEditionWithoutAnIsbn_ComesBackWithoutOne()
    {
        // Null survives the round trip as the fact it is, not as an empty string in disguise.
        var work = AWork(TitleOf("Un pamphlet local"));
        var edition = Edition.Register(EditionId.Generate(), work.Id, isbn: null);

        await SaveAsync(context =>
        {
            new WorkRepository(context).Add(work);
            new EditionRepository(context).Add(edition);
        });

        await using var reader = sqlServer.NewContext();
        (await new EditionRepository(reader).GetByIdAsync(edition.Id, Token))
            .ShouldNotBeNull().Isbn.ShouldBeNull();
    }

    [Fact]
    public async Task AnAbsorbedEdition_ComesBackPointingAtItsSurvivor()
    {
        // The terminal state has to survive the round trip through a value converter, and the
        // record has to still be *there*: downstream contexts hold this identifier, so the row
        // stays and stops being a record rather than being deleted.
        var work = AWork(TitleOf("Les Années"));
        var surviving = Edition.Register(EditionId.Generate(), work.Id, isbn: null);
        var absorbed = Edition.Register(EditionId.Generate(), work.Id, isbn: null);

        await SaveAsync(context =>
        {
            new WorkRepository(context).Add(work);
            new EditionRepository(context).Add(surviving);
            new EditionRepository(context).Add(absorbed);
        });

        await using (var updating = sqlServer.NewContext())
        {
            var repository = new EditionRepository(updating);
            var loaded = await repository.GetByIdAsync(absorbed.Id, Token);
            var survivor = await repository.GetByIdAsync(surviving.Id, Token);

            new EditionMergeDomainService()
                .Merge(loaded.ShouldNotBeNull(), survivor.ShouldNotBeNull())
                .HasErrors().ShouldBeFalse();

            await new CatalogUnitOfWork(updating).SaveChangesAsync(Token);
        }

        await using var reader = sqlServer.NewContext();
        var editions = new EditionRepository(reader);

        (await editions.GetByIdAsync(absorbed.Id, Token))
            .ShouldNotBeNull().AbsorbedInto.ShouldBe(surviving.Id);

        (await editions.GetByIdAsync(surviving.Id, Token))
            .ShouldNotBeNull().AbsorbedInto.ShouldBeNull();
    }

    [Fact]
    public async Task AWorksExistence_IsAnsweredWithoutLoadingIt()
    {
        var work = AWork(TitleOf("Mille plateaux"));
        await SaveAsync(context => new WorkRepository(context).Add(work));

        await using var reader = sqlServer.NewContext();
        var works = new WorkRepository(reader);

        (await works.ExistsAsync(work.Id, Token)).ShouldBeTrue();
        (await works.ExistsAsync(WorkId.Generate(), Token)).ShouldBeFalse();
    }

    private async Task SaveAsync(Action<CatalogDbContext> work)
    {
        await using var context = sqlServer.NewContext();
        work(context);

        await new CatalogUnitOfWork(context).SaveChangesAsync(Token);
    }
}
