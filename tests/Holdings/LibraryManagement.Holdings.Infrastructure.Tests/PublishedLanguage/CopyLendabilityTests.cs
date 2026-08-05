using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.PublishedLanguage;
using LibraryManagement.Holdings.PublishedLanguage;

namespace LibraryManagement.Holdings.Infrastructure.Tests.PublishedLanguage;

/// <summary>
/// The surface Holdings publishes to Circulation, exercised against a real store.
/// </summary>
/// <remarks>
/// It earns integration tests for the reason every published port does: everything that can go
/// wrong here is the store's, starting with the identifier comparison a value-converted key
/// refuses to translate. The port re-states <c>Copy.MayBeLent</c> as a projection, and these
/// tests are what keep the two in step.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class CopyLendabilityTests(SqlServerFixture sqlServer)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static Copy ACopy(EditionId editionId, bool referenceOnly = false)
        => Copy.Acquire(
            CopyId.Generate(),
            editionId,
            Barcode.Create(Unique()).Match(b => b, _ => throw new InvalidOperationException()),
            Shelfmark.Create("843.912 SAI").Match(m => m, _ => throw new InvalidOperationException()),
            CopyCondition.Good,
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
    public async Task ACopyInService_IsLendable_AndNamesItsEdition()
    {
        // The edition rides the answer because the asker's next question is which queue this copy
        // feeds — one desk scan, one round trip.
        var editionId = EditionId.Generate();
        var copy = await StoredAsync(ACopy(editionId));

        await using var reading = sqlServer.NewContext();
        var answer = await new CopyLendability(reading).OfAsync(copy.Id.Value, Token);

        answer.Lendability.ShouldBe(Lendability.Lendable);
        answer.EditionId.ShouldBe(editionId.Value);
    }

    [Fact]
    public async Task ACopyHeldBack_IsNotLendable_ButStillNamesItsEdition()
    {
        var editionId = EditionId.Generate();
        var copy = await StoredAsync(ACopy(editionId, referenceOnly: true));

        await using var reading = sqlServer.NewContext();
        var answer = await new CopyLendability(reading).OfAsync(copy.Id.Value, Token);

        answer.Lendability.ShouldBe(Lendability.NotLendable);
        answer.EditionId.ShouldBe(editionId.Value);
    }

    [Fact]
    public async Task ACopyNobodyAccessioned_IsItsOwnAnswer()
    {
        await using var reading = sqlServer.NewContext();
        var answer = await new CopyLendability(reading).OfAsync(Guid.CreateVersion7(), Token);

        answer.Lendability.ShouldBe(Lendability.NoSuchCopy);
        answer.EditionId.ShouldBeNull();
    }

    [Fact]
    public async Task TheLendableCopiesOfAnEdition_AreTheInServiceOnesAlone()
    {
        // Lendability, never availability: which of these are out on loan is the asker's own
        // arithmetic, and this list must not attempt it.
        var editionId = EditionId.Generate();
        var inService = await StoredAsync(ACopy(editionId));
        await StoredAsync(ACopy(editionId, referenceOnly: true));
        await StoredAsync(ACopy(EditionId.Generate()));

        await using var reading = sqlServer.NewContext();
        var lendable = await new CopyLendability(reading)
            .LendableCopiesOfAsync(editionId.Value, Token);

        lendable.ShouldBe([inService.Id.Value]);
    }
}
