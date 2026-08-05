using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.Serialization;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.Outbox;

namespace LibraryManagement.Members.Infrastructure.Tests.Serialization;

/// <summary>
/// The serializer against this module's real events — the ones the outbox will actually store.
/// </summary>
/// <remarks>
/// Round trips are asserted by record equality, which covers every component including
/// <c>EventId</c> and <c>OccurredOn</c>. Two of this module's converters write objects rather
/// than strings — the first in the solution — so the round trips here are also what pins the
/// payload shape those objects are contract for.
/// </remarks>
public sealed class JsonDomainEventSerializerTests
{
    private static JsonDomainEventSerializer Serializer()
        => new([
            new CardNumberJsonConverter(),
            new MemberNameJsonConverter(),
            new ContactDetailsJsonConverter(),
            new GuardianJsonConverter(),
        ]);

    private static MemberName Name(string given, string family)
        => MemberName.Create(given, family)
            .Match(name => name, _ => throw new InvalidOperationException());

    private static CardNumber Card(string value)
        => CardNumber.Create(value).Match(number => number, _ => throw new InvalidOperationException());

    private static ContactDetails Contact(string? email = null, string? phone = null)
        => ContactDetails.Create(email, phone)
            .Match(contact => contact, _ => throw new InvalidOperationException());

    private static IDomainEvent RoundTrip(IDomainEvent domainEvent)
    {
        var serializer = Serializer();
        var stored = serializer.Serialize(domainEvent);
        return serializer.Deserialize(stored.Type, stored.Payload);
    }

    [Fact]
    public void AnEnrollment_RoundTrips()
    {
        var original = new MemberEnrolled(MemberId.Generate(), MemberCategory.Student, Card("20260000512"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ARenewal_RoundTripsItsNewEnd()
    {
        var original = new MembershipRenewed(MemberId.Generate(), new DateOnly(2027, 3, 14));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ACategoryChange_RoundTripsBothCategories()
    {
        var original = new MemberCategoryChanged(
            MemberId.Generate(), MemberCategory.Child, MemberCategory.Adult);

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ACardReplacement_RoundTripsBothNumbers()
    {
        var original = new CardReplaced(MemberId.Generate(), Card("20260000512"), Card("20260000999"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AContactChange_RoundTripsTheChannels()
    {
        var original = new ContactDetailsChanged(
            MemberId.Generate(), Contact("antoine.doinel@example.org", "0612345678"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AContactChangeToNoChannelAtAll_RoundTripsTheAbsence()
    {
        // The cleared channels are part of the contract: the projection reads the absence from
        // the payload, so the payload must be able to say it.
        var original = new ContactDetailsChanged(MemberId.Generate(), ContactDetails.None);

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AGuardianChange_RoundTripsTheWholeGuardian()
    {
        // The one payload that nests: a name and channels, each through its own converter, which
        // is exactly what composing rather than flattening is supposed to buy.
        var original = new GuardianChanged(
            MemberId.Generate(),
            Guardian.Of(Name("Gilberte", "Doinel"), Contact("gilberte.doinel@example.org")));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AGuardianRemoval_RoundTripsTheAbsence()
    {
        var original = new GuardianChanged(MemberId.Generate(), NewGuardian: null);

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ARenaming_RoundTripsBothNames()
    {
        var original = new MemberRenamed(
            MemberId.Generate(), Name("Antoine", "Doinel"), Name("Antoine", "Doinel-Montag"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void TheTypeName_CarriesNoVersion()
    {
        // The stored name is the address deserialization dials, and an address that included the
        // assembly version would orphan every row at the next version bump.
        var stored = Serializer().Serialize(
            new MembershipRenewed(MemberId.Generate(), new DateOnly(2027, 3, 14)));

        stored.Type.ShouldBe(
            "LibraryManagement.Members.Domain.Members.MembershipRenewed, LibraryManagement.Members.Domain");
    }

    [Fact]
    public void ACardNumberTheDomainRefuses_IsCorruptionOnTheWayBack()
    {
        // Trust-the-store, loudly: reading goes back through Create, and a payload Create refuses
        // is a row that lies. The repair belongs to a migration, not to a silent skip.
        var thrown = Should.Throw<InvalidOperationException>(() => Serializer().Deserialize(
            "LibraryManagement.Members.Domain.Members.CardReplaced, LibraryManagement.Members.Domain",
            """{"MemberId":"0198c0de-0000-7000-8000-000000000001","PreviousCardNumber":"20260000512","NewCardNumber":"1"}"""));

        thrown.Message.ShouldContain("card number");
    }

    [Fact]
    public void AGuardianWithoutANameOrChannels_IsCorruptionOnTheWayBack()
    {
        var thrown = Should.Throw<InvalidOperationException>(() => Serializer().Deserialize(
            "LibraryManagement.Members.Domain.Members.GuardianChanged, LibraryManagement.Members.Domain",
            """{"MemberId":"0198c0de-0000-7000-8000-000000000001","NewGuardian":{"Name":null,"Contact":null}}"""));

        thrown.Message.ShouldContain("guardian");
    }
}
