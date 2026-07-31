using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Search;

/// <summary>
/// The access-point index, one projector at a time, against the real store.
/// </summary>
/// <remarks>
/// Each test plays events at a projector and saves once, the way the outbox drain does — the
/// projectors themselves never save. Idempotence is exercised deliberately: delivery is
/// at-least-once, so a redelivered event must land on the state it already produced.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class AccessPointProjectionTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NameForm Name(string value)
        => NameForm.Create(value).Match(name => name, _ => throw new InvalidOperationException());

    private static Title TitleOf(string value)
        => Title.Create(value).Match(title => title, _ => throw new InvalidOperationException());

    private async Task<List<AccessPoint>> FormsOfAsync(Guid targetId)
    {
        await using var reader = sqlServer.NewContext();
        return await reader.Set<AccessPoint>()
            .AsNoTracking()
            .Where(accessPoint => accessPoint.TargetId == targetId)
            .OrderBy(accessPoint => accessPoint.Form)
            .ToListAsync(Token);
    }

    // --- Authors ---------------------------------------------------------------------------------

    [Fact]
    public async Task ARegisteredAuthor_IsFindableByItsPreferredName()
    {
        var authorId = AuthorId.Generate();

        await using (var context = sqlServer.NewContext())
        {
            await new AuthorRegisteredProjector(context)
                .Handle(new AuthorRegistered(authorId, Name("Ernaux, Annie")), Token);
            await context.SaveChangesAsync(Token);
        }

        var form = (await FormsOfAsync(authorId.Value)).ShouldHaveSingleItem();
        form.Form.ShouldBe("Ernaux, Annie");
        form.IsPreferred.ShouldBeTrue();
        form.Kind.ShouldBe(AccessPointKind.Author);
    }

    [Fact]
    public async Task ARename_PromotesTheIncomingFormAndKeepsTheOutgoingOneFindable()
    {
        var authorId = AuthorId.Generate();

        await using (var context = sqlServer.NewContext())
        {
            await new AuthorRegisteredProjector(context)
                .Handle(new AuthorRegistered(authorId, Name("Smith, Jane")), Token);
            await new AuthorRenamedProjector(context)
                .Handle(new AuthorRenamed(authorId, Name("Smith, Jane"), Name("Smith, Alex")), Token);
            await context.SaveChangesAsync(Token);
        }

        var forms = await FormsOfAsync(authorId.Value);
        forms.Single(form => form.Form == "Smith, Alex").IsPreferred.ShouldBeTrue();
        forms.Single(form => form.Form == "Smith, Jane").IsPreferred.ShouldBeFalse();
    }

    [Fact]
    public async Task ACorrection_RetractsTheWrongFormOutright()
    {
        // The difference a rename never has: the typo stops being findable. Kept as an access
        // point, it would preserve forever the one thing the catalog was asked to remove.
        var authorId = AuthorId.Generate();

        await using (var context = sqlServer.NewContext())
        {
            await new AuthorRegisteredProjector(context)
                .Handle(new AuthorRegistered(authorId, Name("Ernuax, Annie")), Token);
            await new AuthorPreferredNameCorrectedProjector(context)
                .Handle(new AuthorPreferredNameCorrected(authorId, Name("Ernuax, Annie"), Name("Ernaux, Annie")), Token);
            await context.SaveChangesAsync(Token);
        }

        var form = (await FormsOfAsync(authorId.Value)).ShouldHaveSingleItem();
        form.Form.ShouldBe("Ernaux, Annie");
        form.IsPreferred.ShouldBeTrue();
    }

    [Fact]
    public async Task AVariant_LeadsBackToTheRecord()
    {
        var authorId = AuthorId.Generate();

        await using (var context = sqlServer.NewContext())
        {
            await new AuthorRegisteredProjector(context)
                .Handle(new AuthorRegistered(authorId, Name("Gary, Romain")), Token);
            await new AuthorVariantNameAddedProjector(context)
                .Handle(new AuthorVariantNameAdded(authorId, Name("Ajar, Émile")), Token);
            await context.SaveChangesAsync(Token);
        }

        var forms = await FormsOfAsync(authorId.Value);
        forms.Count.ShouldBe(2);
        forms.Single(form => form.Form == "Ajar, Émile").IsPreferred.ShouldBeFalse();
    }

    [Fact]
    public async Task TwoAuthorsSharingAForm_AreTwoAnswers()
    {
        // Homonyms are ordinary in a catalog: the form resolves to every record it leads to, and
        // telling them apart is what the authority record exists for.
        var first = AuthorId.Generate();
        var second = AuthorId.Generate();

        await using (var context = sqlServer.NewContext())
        {
            var projector = new AuthorRegisteredProjector(context);
            await projector.Handle(new AuthorRegistered(first, Name("Smith, Jane")), Token);
            await projector.Handle(new AuthorRegistered(second, Name("Smith, Jane")), Token);
            await context.SaveChangesAsync(Token);
        }

        (await FormsOfAsync(first.Value)).ShouldHaveSingleItem();
        (await FormsOfAsync(second.Value)).ShouldHaveSingleItem();
    }

    // --- Works -----------------------------------------------------------------------------------

    [Fact]
    public async Task ARegisteredWork_IsFindableByItsTitle()
    {
        var workId = WorkId.Generate();

        await using (var context = sqlServer.NewContext())
        {
            await new WorkRegisteredProjector(context)
                .Handle(new WorkRegistered(workId, TitleOf("Mille plateaux")), Token);
            await context.SaveChangesAsync(Token);
        }

        var form = (await FormsOfAsync(workId.Value)).ShouldHaveSingleItem();
        form.Form.ShouldBe("Mille plateaux");
        form.Kind.ShouldBe(AccessPointKind.Work);
    }

    [Fact]
    public async Task ARetitle_ReplacesTheTitleOutright()
    {
        // A replacement and not a demotion, mirroring the model: a retitled work keeps no memory of
        // its former title, so neither does the index.
        var workId = WorkId.Generate();

        await using (var context = sqlServer.NewContext())
        {
            await new WorkRegisteredProjector(context)
                .Handle(new WorkRegistered(workId, TitleOf("Mille plateaux")), Token);
            await new WorkRetitledProjector(context)
                .Handle(new WorkRetitled(workId, TitleOf("Mille plateaux"), TitleOf("A Thousand Plateaus")), Token);
            await context.SaveChangesAsync(Token);
        }

        var form = (await FormsOfAsync(workId.Value)).ShouldHaveSingleItem();
        form.Form.ShouldBe("A Thousand Plateaus");
    }

    // --- Editions --------------------------------------------------------------------------------

    [Fact]
    public async Task ARegisteredEdition_IsFindableByItsIsbn()
    {
        var editionId = EditionId.Generate();
        var isbn = Isbn.Create("9782070612758").Match(value => value, _ => throw new InvalidOperationException());

        await using (var context = sqlServer.NewContext())
        {
            await new EditionRegisteredProjector(context)
                .Handle(new EditionRegistered(editionId, WorkId.Generate(), isbn), Token);
            await context.SaveChangesAsync(Token);
        }

        var form = (await FormsOfAsync(editionId.Value)).ShouldHaveSingleItem();
        form.Form.ShouldBe("9782070612758");
        form.IsPreferred.ShouldBeTrue();
        form.Kind.ShouldBe(AccessPointKind.Edition);
    }

    [Fact]
    public async Task AnEditionWithoutAnIsbn_AddsNothing()
    {
        // No form to answer to is a fact about the edition, not an error of the projector.
        var editionId = EditionId.Generate();

        await using (var context = sqlServer.NewContext())
        {
            await new EditionRegisteredProjector(context)
                .Handle(new EditionRegistered(editionId, WorkId.Generate(), Isbn: null), Token);
            await context.SaveChangesAsync(Token);
        }

        (await FormsOfAsync(editionId.Value)).ShouldBeEmpty();
    }

    // --- At-least-once ---------------------------------------------------------------------------

    [Fact]
    public async Task ARedeliveredEvent_LandsOnTheStateItAlreadyProduced()
    {
        // The outbox promises at-least-once, so every projector converges rather than accumulates.
        // The second delivery arrives in a later save, the way a real redelivery would.
        var authorId = AuthorId.Generate();
        var registered = new AuthorRegistered(authorId, Name("Perec, Georges"));

        await using (var context = sqlServer.NewContext())
        {
            await new AuthorRegisteredProjector(context).Handle(registered, Token);
            await context.SaveChangesAsync(Token);
        }

        await using (var context = sqlServer.NewContext())
        {
            await new AuthorRegisteredProjector(context).Handle(registered, Token);
            await context.SaveChangesAsync(Token);
        }

        (await FormsOfAsync(authorId.Value)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ARedeliveredRetraction_FindsNothingLeftToRetract()
    {
        var authorId = AuthorId.Generate();
        var corrected = new AuthorPreferredNameCorrected(authorId, Name("Ernuax, Annie"), Name("Ernaux, Annie"));

        await using (var context = sqlServer.NewContext())
        {
            await new AuthorRegisteredProjector(context)
                .Handle(new AuthorRegistered(authorId, Name("Ernuax, Annie")), Token);
            await new AuthorPreferredNameCorrectedProjector(context).Handle(corrected, Token);
            await context.SaveChangesAsync(Token);
        }

        await using (var context = sqlServer.NewContext())
        {
            await new AuthorPreferredNameCorrectedProjector(context).Handle(corrected, Token);
            await context.SaveChangesAsync(Token);
        }

        (await FormsOfAsync(authorId.Value)).ShouldHaveSingleItem().Form.ShouldBe("Ernaux, Annie");
    }
}
