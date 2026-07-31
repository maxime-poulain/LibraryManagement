using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Persistence;
using LibraryManagement.Holdings.Infrastructure.PublishedLanguage;
using LibraryManagement.Holdings.PublishedLanguage;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Holdings.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CopyPersistenceTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Barcode ABarcode(string value) =>
        Barcode.Create(value).Match(barcode => barcode, _ => throw new InvalidOperationException(value));

    private static Shelfmark AShelfmark(string value = "843.912 SAI") =>
        Shelfmark.Create(value).Match(mark => mark, _ => throw new InvalidOperationException(value));

    // Version 4 rather than 7: these are labels, not identifiers, and three generated in one
    // millisecond must not share a prefix — which is exactly what a time-ordered UUID guarantees
    // they would.
    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static Copy ACopy(string barcode, bool referenceOnly = false)
        => Copy.Acquire(
            CopyId.Generate(),
            EditionId.Generate(),
            ABarcode(barcode),
            AShelfmark(),
            CopyCondition.Worn,
            new DateOnly(2024, 3, 14),
            referenceOnly);

    private async Task<Copy> StoredAsync(Copy copy)
    {
        await using var writing = sqlServer.NewContext();
        writing.Add(copy);
        await writing.SaveChangesAsync(Token);
        return copy;
    }

    [Fact]
    public async Task ACopy_ComesBackWithEverythingItWasStoredWith()
    {
        var copy = await StoredAsync(ACopy(Unique()));

        // A second context, so this proves the values reached the database rather than the change
        // tracker.
        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Copy>().SingleAsync(stored => stored.Id == copy.Id, Token);

        found.EditionId.ShouldBe(copy.EditionId);
        found.Barcode.ShouldBe(copy.Barcode);
        found.Shelfmark.ShouldBe(copy.Shelfmark);
        found.Condition.ShouldBe(CopyCondition.Worn);
        found.Status.ShouldBe(CopyStatus.InService);
        found.AcquiredOn.ShouldBe(new DateOnly(2024, 3, 14));
    }

    [Fact]
    public async Task ARepairsDestination_SurvivesTheRoundTrip()
    {
        // The one piece of state that exists to prevent an accident, so it is the one worth proving
        // the store actually keeps.
        var copy = ACopy(Unique(), referenceOnly: true);
        copy.SendForRepair();
        await StoredAsync(copy);

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Copy>().SingleAsync(stored => stored.Id == copy.Id, Token);

        found.Status.ShouldBe(CopyStatus.InRepair);
        found.ReturnsTo.ShouldBe(CopyStatus.ReferenceOnly);

        found.ReturnFromRepair();
        found.Status.ShouldBe(CopyStatus.ReferenceOnly);
    }

    [Fact]
    public async Task ACopyNotInRepair_HoldsNoDestination()
    {
        var copy = await StoredAsync(ACopy(Unique()));

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Copy>().SingleAsync(stored => stored.Id == copy.Id, Token);

        found.ReturnsTo.ShouldBeNull();
    }

    [Fact]
    public async Task TwoCopiesSharingABarcode_AreRefusedByTheStore()
    {
        // The handler asks first so the refusal can name the label. This is what makes the answer
        // still true by the time it is written, and it is the rule's actual holder.
        var barcode = Unique();
        await StoredAsync(ACopy(barcode));

        await using var writing = sqlServer.NewContext();
        writing.Add(ACopy(barcode));

        await Should.ThrowAsync<DbUpdateException>(() => writing.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task TwoCopiesOfOneEdition_AreOrdinary()
    {
        // The common case, and the reason the edition index is not unique: a library buys three.
        var editionId = EditionId.Generate();

        await using var writing = sqlServer.NewContext();

        foreach (var _ in Enumerable.Range(0, 3))
        {
            writing.Add(Copy.Acquire(
                CopyId.Generate(), editionId, ABarcode(Unique()), AShelfmark(),
                CopyCondition.Good, new DateOnly(2024, 3, 14)));
        }

        await writing.SaveChangesAsync(Token);

        await using var reading = sqlServer.NewContext();
        var held = await reading.Set<Copy>().CountAsync(copy => copy.EditionId == editionId, Token);
        held.ShouldBe(3);
    }

    [Fact]
    public async Task AStatus_IsStoredUnderItsName()
    {
        // A projection table is read by a human when something looks wrong, and 'InRepair' answers
        // where '1' asks.
        var copy = ACopy(Unique());
        copy.SendForRepair();
        await StoredAsync(copy);

        await using var reading = sqlServer.NewContext();
        var stored = await reading.Database
            .SqlQuery<string>($"SELECT Status AS Value FROM holdings.Copy WHERE Id = {copy.Id.Value}")
            .SingleAsync(Token);

        stored.ShouldBe("InRepair");
    }

    [Fact]
    public async Task TheModule_MapsOntoItsOwnSchema()
    {
        // The second schema in the solution, and the first time the separation is more than a
        // principle.
        await using var reading = sqlServer.NewContext();
        var schemas = await reading.Database
            .SqlQuery<string>(
                $"SELECT DISTINCT TABLE_SCHEMA AS Value FROM INFORMATION_SCHEMA.TABLES")
            .ToListAsync(Token);

        schemas.ShouldContain(HoldingsDbContext.Schema);
    }

    [Fact]
    public async Task Lendability_AnswersTheSameThingTheAggregateDoes()
    {
        // The published language projects a rule the domain states, and a projection that drifts
        // from its rule is worse than none. This is what keeps the two in step.
        var lendable = await StoredAsync(ACopy(Unique()));
        var reference = await StoredAsync(ACopy(Unique(), referenceOnly: true));

        await using var reading = sqlServer.NewContext();
        var lendability = new CopyLendability(reading);

        (await lendability.OfAsync(lendable.Id.Value, Token)).ShouldBe(Lendability.Lendable);
        lendable.MayBeLent().ShouldBeTrue();

        (await lendability.OfAsync(reference.Id.Value, Token)).ShouldBe(Lendability.NotLendable);
        reference.MayBeLent().ShouldBeFalse();
    }

    [Fact]
    public async Task Lendability_ACopyNobodyHolds_IsNotARefusal()
    {
        // Three answers and not two: a barcode matching nothing is a mis-scan or a copy never
        // accessioned, and a librarian handles that differently from a copy in repair.
        await using var reading = sqlServer.NewContext();

        var answer = await new CopyLendability(reading).OfAsync(Guid.CreateVersion7(), Token);

        answer.ShouldBe(Lendability.NoSuchCopy);
    }
}
