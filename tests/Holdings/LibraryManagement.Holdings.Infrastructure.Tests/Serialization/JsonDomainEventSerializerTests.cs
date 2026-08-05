using LibraryManagement.Holdings.Domain.Copies;
using LibraryManagement.Holdings.Infrastructure.Serialization;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.Outbox;

namespace LibraryManagement.Holdings.Infrastructure.Tests.Serialization;

/// <summary>
/// The serializer against this module's real events — the ones the outbox will actually store.
/// </summary>
/// <remarks>
/// Round trips are asserted by record equality, which covers every component including
/// <c>EventId</c> and <c>OccurredOn</c>: a redelivered event must carry the identity it was
/// raised with, or deduplication has nothing to hold on to.
/// </remarks>
public sealed class JsonDomainEventSerializerTests
{
    private static JsonDomainEventSerializer Serializer()
        => new([new BarcodeJsonConverter(), new ShelfmarkJsonConverter()]);

    private static Barcode Label(string value)
        => Barcode.Create(value).Match(barcode => barcode, _ => throw new InvalidOperationException());

    private static Shelfmark Mark(string value)
        => Shelfmark.Create(value).Match(mark => mark, _ => throw new InvalidOperationException());

    private static IDomainEvent RoundTrip(IDomainEvent domainEvent)
    {
        var serializer = Serializer();
        var stored = serializer.Serialize(domainEvent);
        return serializer.Deserialize(stored.Type, stored.Payload);
    }

    [Fact]
    public void AnAcquisition_RoundTrips()
    {
        var original = new CopyAcquired(CopyId.Generate(), EditionId.Generate(), Label("30124000512"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AMove_RoundTripsBothShelfmarks()
    {
        var original = new CopyReshelved(
            CopyId.Generate(), Mark("843.912 SAI"), Mark("JEUN 843.912 SAI"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ARelabelling_RoundTripsBothLabels()
    {
        var original = new CopyRelabelled(
            CopyId.Generate(), Label("30124000512"), Label("30124000999"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AConditionRecord_RoundTripsBothConditions()
    {
        var original = new CopyConditionRecorded(
            CopyId.Generate(), CopyCondition.Good, CopyCondition.Worn);

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ARepairDeparture_RoundTrips()
    {
        var original = new CopySentForRepair(CopyId.Generate());

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ARepairReturn_RoundTripsItsDestination()
    {
        // Not always InService: a reference-only copy sent for rebinding comes back
        // reference-only, and the payload is where a consumer learns it.
        var original = new CopyReturnedFromRepair(CopyId.Generate(), CopyStatus.ReferenceOnly);

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void TheCuratorialPair_RoundTrips()
    {
        var restricted = new CopyRestrictedToReference(CopyId.Generate());
        var released = new CopyReleasedForLending(CopyId.Generate());

        RoundTrip(restricted).ShouldBe(restricted);
        RoundTrip(released).ShouldBe(released);
    }

    [Fact]
    public void ALossAndAFind_RoundTrip()
    {
        var lost = new CopyDeclaredLost(CopyId.Generate());
        var found = new CopyFound(CopyId.Generate(), CopyStatus.InService);

        RoundTrip(lost).ShouldBe(lost);
        RoundTrip(found).ShouldBe(found);
    }

    [Fact]
    public void AWithdrawal_RoundTrips()
    {
        var original = new CopyWithdrawn(CopyId.Generate());

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void TheTypeName_CarriesNoVersion()
    {
        // The stored name is the address deserialization dials, and an address that included the
        // assembly version would orphan every row at the next version bump.
        var stored = Serializer().Serialize(new CopyWithdrawn(CopyId.Generate()));

        stored.Type.ShouldBe(
            "LibraryManagement.Holdings.Domain.Copies.CopyWithdrawn, LibraryManagement.Holdings.Domain");
    }

    [Fact]
    public void ABarcodeTheDomainRefuses_IsCorruptionOnTheWayBack()
    {
        // Trust-the-store, loudly: reading goes back through Create, and a payload Create refuses
        // is a row that lies. The repair belongs to a migration, not to a silent skip.
        var thrown = Should.Throw<InvalidOperationException>(() => Serializer().Deserialize(
            "LibraryManagement.Holdings.Domain.Copies.CopyRelabelled, LibraryManagement.Holdings.Domain",
            """{"CopyId":"0198c0de-0000-7000-8000-000000000001","PreviousBarcode":"30124000512","NewBarcode":"1"}"""));

        thrown.Message.ShouldContain("barcode");
    }

    [Fact]
    public void AShelfmarkTheDomainRefuses_IsCorruptionOnTheWayBack()
    {
        var thrown = Should.Throw<InvalidOperationException>(() => Serializer().Deserialize(
            "LibraryManagement.Holdings.Domain.Copies.CopyReshelved, LibraryManagement.Holdings.Domain",
            """{"CopyId":"0198c0de-0000-7000-8000-000000000001","PreviousShelfmark":"843.912 SAI","NewShelfmark":"  "}"""));

        thrown.Message.ShouldContain("shelfmark");
    }
}
