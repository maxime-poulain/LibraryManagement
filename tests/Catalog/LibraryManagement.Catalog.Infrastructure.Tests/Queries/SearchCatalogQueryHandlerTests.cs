using LibraryManagement.Catalog.Application.Search.SearchCatalog;
using LibraryManagement.Catalog.Infrastructure.Queries;
using LibraryManagement.Catalog.Infrastructure.Search;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Queries;

/// <summary>
/// The search, asked of the real store.
/// </summary>
/// <remarks>
/// Rows are seeded directly rather than played through the projectors: the table is the read
/// side's contract, the projectors have tests of their own, and what is under test here is only
/// how the index is read back.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class SearchCatalogQueryHandlerTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Every form this class seeds starts with a stem no other test can produce. The search is a
    // prefix match and the database outlives the test, so the stem is at once the isolation and
    // the search term. Version 4 and not version 7, deliberately: a v7 identifier leads with the
    // timestamp, so every stem minted in one run would share its first characters — and a shared
    // prefix is exactly what a prefix search must never be handed by accident.
    private static string AStem() => $"Zz{Guid.NewGuid():N}"[..10];

    private static AccessPoint APoint(AccessPointKind kind, Guid targetId, string form, bool preferred)
        => new() { Kind = kind, TargetId = targetId, Form = form, IsPreferred = preferred };

    private async Task SeedAsync(params AccessPoint[] points)
    {
        await using var context = sqlServer.NewContext();
        context.Set<AccessPoint>().AddRange(points);
        await context.SaveChangesAsync(Token);
    }

    private async Task<IReadOnlyList<CatalogEntryDto>> SearchAsync(string term)
    {
        await using var reader = sqlServer.NewContext();

        var result = await new SearchCatalogQueryHandler(reader)
            .Handle(new SearchCatalogQuery(term), Token);

        return result.Match(entries => entries, _ => throw new InvalidOperationException("Expected success."));
    }

    [Fact]
    public async Task APreferredName_IsItsOwnEntry()
    {
        var stem = AStem();
        var authorId = Guid.CreateVersion7();
        await SeedAsync(APoint(AccessPointKind.Author, authorId, $"{stem}, Annie", preferred: true));

        var entry = (await SearchAsync(stem)).ShouldHaveSingleItem();

        entry.Kind.ShouldBe(CatalogEntryKind.Author);
        entry.RecordId.ShouldBe(authorId);
        entry.Form.ShouldBe($"{stem}, Annie");
        entry.PreferredForm.ShouldBe(entry.Form);
    }

    [Fact]
    public async Task AVariantMatch_IsASeeReference()
    {
        // The line a variant exists to produce: the form that matched, and the preferred name it leads
        // to — Sullivan, Vernon *see* Vian, Boris.
        var stem = AStem();
        var authorId = Guid.CreateVersion7();
        var preferred = $"Gary, Romain ({stem})";
        await SeedAsync(
            APoint(AccessPointKind.Author, authorId, preferred, preferred: true),
            APoint(AccessPointKind.Author, authorId, $"{stem} Ajar, Émile", preferred: false));

        var entry = (await SearchAsync(stem)).ShouldHaveSingleItem();

        entry.Form.ShouldBe($"{stem} Ajar, Émile");
        entry.PreferredForm.ShouldBe(preferred);
    }

    [Fact]
    public async Task TheSearch_ReadsAPrefixAndNotASubstring()
    {
        // The browse behaviour of a catalog: preferred names are filed surname-first and titles as
        // printed precisely so the beginning is the search. A middle is not a way in.
        var stem = AStem();
        await SeedAsync(APoint(AccessPointKind.Author, Guid.CreateVersion7(), $"{stem}-Ernaux, Annie", preferred: true));

        (await SearchAsync(stem)).ShouldHaveSingleItem();
        (await SearchAsync("-Ernaux, Annie")).ShouldBeEmpty();
    }

    [Fact]
    public async Task AWildcardInTheTerm_MeansTheCharacter()
    {
        // The term is typed by an employee, so '%' is punctuation and never a pattern. Unescaped,
        // the second search would read LIKE '%…%' and match this very row by containment.
        var stem = AStem();
        await SeedAsync(APoint(AccessPointKind.Work, Guid.CreateVersion7(), $"{stem} 100% vrai", preferred: true));

        (await SearchAsync($"{stem} 100%")).ShouldHaveSingleItem();
        (await SearchAsync($"%{stem}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task HomonymousRecords_AreAsManyEntries()
    {
        // Two authors filed under one form are two answers, and telling them apart is what the
        // authority record exists for.
        var stem = AStem();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();
        await SeedAsync(
            APoint(AccessPointKind.Author, first, $"{stem}, Jane", preferred: true),
            APoint(AccessPointKind.Author, second, $"{stem}, Jane", preferred: true));

        var entries = await SearchAsync(stem);

        entries.Count.ShouldBe(2);
        entries.Select(entry => entry.RecordId).ShouldBe([first, second], ignoreOrder: true);
    }

    [Fact]
    public async Task AuthorsAndWorks_InterfileInFilingOrder()
    {
        // The dictionary catalog: one alphabet for persons and titles, so whoever typed the
        // term does not have to say what kind of thing they are looking for before they may look.
        var stem = AStem();
        await SeedAsync(
            APoint(AccessPointKind.Work, Guid.CreateVersion7(), $"{stem} bb, the title", preferred: true),
            APoint(AccessPointKind.Author, Guid.CreateVersion7(), $"{stem} aa, the person", preferred: true));

        var entries = await SearchAsync(stem);

        entries.Count.ShouldBe(2);
        entries[0].Kind.ShouldBe(CatalogEntryKind.Author);
        entries[1].Kind.ShouldBe(CatalogEntryKind.Work);
    }

    [Fact]
    public async Task TheAnswer_StopsAtTheCap()
    {
        // The first page of the browse, in filing order. Paging waits for the interface that
        // needs it; the cap only keeps a one-letter search from carting the index across.
        var stem = AStem();
        var beyondTheCap = SearchCatalogQuery.MaxResults + 10;
        await SeedAsync([.. Enumerable.Range(0, beyondTheCap)
            .Select(i => APoint(AccessPointKind.Work, Guid.CreateVersion7(), $"{stem} {i:D3}", preferred: true))]);

        var entries = await SearchAsync(stem);

        entries.Count.ShouldBe(SearchCatalogQuery.MaxResults);
        entries[0].Form.ShouldBe($"{stem} 000");
        entries[^1].Form.ShouldBe($"{stem} {SearchCatalogQuery.MaxResults - 1:D3}");
    }

    [Fact]
    public async Task AnIsbn_LeadsToItsEdition()
    {
        // The third kind of line the index carries, mapped to the contract's own enum: an ISBN is
        // an access point exactly as a preferred name or a title is.
        var stem = AStem();
        var editionId = Guid.CreateVersion7();
        await SeedAsync(APoint(AccessPointKind.Edition, editionId, $"{stem}9782070612758", preferred: true));

        var entry = (await SearchAsync(stem)).ShouldHaveSingleItem();

        entry.Kind.ShouldBe(CatalogEntryKind.Edition);
        entry.RecordId.ShouldBe(editionId);
    }

    [Fact]
    public async Task TheMatch_IsCaseInsensitive()
    {
        // The collation's decision, not this handler's — and this test is what pins it, so a
        // database set up case-sensitively fails loudly instead of quietly answering less.
        var stem = AStem();
        await SeedAsync(APoint(AccessPointKind.Author, Guid.CreateVersion7(), $"{stem}, Annie", preferred: true));

        (await SearchAsync(stem.ToUpperInvariant())).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task TheHandler_DoesNotSecondGuessTheValidator()
    {
        // A blank term is rejected at dispatch; reaching the handler with one is a wiring
        // mistake, reported as one — a prefix of nothing would match the entire index.
        await using var reader = sqlServer.NewContext();

        await Should.ThrowAsync<ArgumentException>(
            async () => await new SearchCatalogQueryHandler(reader)
                .Handle(new SearchCatalogQuery("   "), Token));
    }
}
