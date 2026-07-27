using LibraryManagement.Shared.Domain.Tests.TestDoubles;

namespace LibraryManagement.Shared.Domain.Tests;

public sealed class DomainEventTests
{
    [Fact]
    public void EventId_IsPopulated()
    {
        new TestDomainEvent("payload").EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void EventId_IsAVersion7Uuid()
    {
        var version = new TestDomainEvent("payload").EventId.ToByteArray(bigEndian: true)[6] >> 4;

        version.ShouldBe(7);
    }

    [Fact]
    public void EventId_IsDistinctForEveryOccurrence_EvenWithAnIdenticalPayload()
    {
        var first = new TestDomainEvent("same payload");
        var second = new TestDomainEvent("same payload");

        first.EventId.ShouldNotBe(second.EventId);
    }

    [Fact]
    public void OccurredOn_IsPopulatedWithTheCurrentInstant()
    {
        var before = DateTimeOffset.UtcNow;
        var raised = new TestDomainEvent("payload");
        var after = DateTimeOffset.UtcNow;

        raised.OccurredOn.ShouldBeInRange(before, after);
    }

    [Fact]
    public void RecordEquality_TreatsTwoOccurrencesOfTheSamePayloadAsDifferent()
    {
        // They differ by EventId, which is part of the record's value.
        new TestDomainEvent("same").Equals(new TestDomainEvent("same")).ShouldBeFalse();
    }

    [Fact]
    public void RecordEquality_TreatsTwoEventsWithTheSameMetadataAndPayloadAsEqual()
    {
        var original = new TestDomainEvent("same");
        var copy = original with { };

        original.Equals(copy).ShouldBeTrue();
    }

    [Fact]
    public void Metadata_CanBePinnedByATest()
    {
        var instant = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var eventId = Guid.CreateVersion7();

        var pinned = new TestDomainEvent("payload") with { EventId = eventId, OccurredOn = instant };

        pinned.EventId.ShouldBe(eventId);
        pinned.OccurredOn.ShouldBe(instant);
        pinned.Payload.ShouldBe("payload");
    }

    [Fact]
    public void ADomainEvent_IsDispatchableAsAMediatorNotification()
    {
        new TestDomainEvent("payload").ShouldBeAssignableTo<Mediator.INotification>();
    }

    [Fact]
    public void TwoEventTypesWithTheSamePayloadShape_AreNotEqual()
    {
        var eventId = Guid.CreateVersion7();
        var instant = DateTimeOffset.UtcNow;

        var first = new TestDomainEvent("payload") with { EventId = eventId, OccurredOn = instant };
        object second = new OtherDomainEvent("payload") with { EventId = eventId, OccurredOn = instant };

        first.Equals(second).ShouldBeFalse();
    }
}
