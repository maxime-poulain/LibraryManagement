using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;
using LibraryManagement.Catalog.Infrastructure.Serialization;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Infrastructure.Outbox;

namespace LibraryManagement.Catalog.Infrastructure.Tests.Serialization;

/// <summary>
/// The serializer against this module's real events — the ones the outbox will actually store.
/// </summary>
/// <remarks>
/// Round trips are asserted by record equality, which covers every component including
/// <c>EventId</c> and <c>OccurredOn</c>: a redelivered event must carry the identity it was raised
/// with, or deduplication has nothing to hold on to.
/// </remarks>
public sealed class JsonDomainEventSerializerTests
{
    private static JsonDomainEventSerializer Serializer()
        => new([new PersonNameJsonConverter(), new TitleJsonConverter(), new IsbnJsonConverter()]);

    private static PersonName Name(string value)
        => PersonName.Create(value).Match(name => name, _ => throw new InvalidOperationException());

    private static Title TitleOf(string value)
        => Title.Create(value).Match(title => title, _ => throw new InvalidOperationException());

    private static IDomainEvent RoundTrip(IDomainEvent domainEvent)
    {
        var serializer = Serializer();
        var stored = serializer.Serialize(domainEvent);
        return serializer.Deserialize(stored.Type, stored.Payload);
    }

    [Fact]
    public void AnAuthorRegistration_RoundTrips()
    {
        var original = new AuthorRegistered(AuthorId.Generate(), Name("Ernaux, Annie"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void ARename_RoundTripsBothNames()
    {
        var original = new AuthorRenamed(AuthorId.Generate(), Name("Smith, Jane"), Name("Smith, Alex"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AHeadingCorrection_RoundTrips()
    {
        var original = new AuthorHeadingCorrected(
            AuthorId.Generate(),
            Name("Ernuax, Annie"),
            Name("Ernaux, Annie"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AWorkRegistration_RoundTrips()
    {
        var original = new WorkRegistered(WorkId.Generate(), TitleOf("Le Petit Prince"));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AnEditionRegistration_RoundTripsItsIsbn()
    {
        var original = new EditionRegistered(
            EditionId.Generate(),
            WorkId.Generate(),
            Isbn.Create("978-2-07-061275-8").Match(isbn => isbn, _ => throw new InvalidOperationException()));

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void AnEditionRegistrationWithoutAnIsbn_RoundTripsTheAbsence()
    {
        // Null is part of the contract: the projection reads the absence from the payload, so the
        // payload must be able to say it.
        var original = new EditionRegistered(EditionId.Generate(), WorkId.Generate(), Isbn: null);

        RoundTrip(original).ShouldBe(original);
    }

    [Fact]
    public void TheTypeName_CarriesNoVersion()
    {
        // The stored name is the address deserialization dials, and an address that included the
        // assembly version would orphan every row at the next version bump.
        var stored = Serializer().Serialize(new AuthorRegistered(AuthorId.Generate(), Name("Anyone")));

        stored.Type.ShouldBe(
            "LibraryManagement.Catalog.Domain.Authors.AuthorRegistered, LibraryManagement.Catalog.Domain");
    }

    [Fact]
    public void ATypeThatNoLongerExists_IsRefusedByName()
    {
        // Trust-the-store, loudly: a row that cannot be read is corruption, and the repair is a
        // migration rewriting the stored names, not a silent skip.
        var thrown = Should.Throw<InvalidOperationException>(
            () => Serializer().Deserialize("LibraryManagement.Catalog.Domain.Authors.Renamed, Gone", "{}"));

        thrown.Message.ShouldContain("no longer exists");
    }
}
