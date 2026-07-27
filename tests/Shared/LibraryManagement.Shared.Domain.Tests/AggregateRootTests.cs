using LibraryManagement.Shared.Domain.Tests.TestDoubles;

namespace LibraryManagement.Shared.Domain.Tests;

public sealed class AggregateRootTests
{
    private static TestAggregate NewAggregate() => new(TestEntityId.Generate());

    [Fact]
    public void DomainEvents_OnAFreshAggregate_IsEmpty()
    {
        NewAggregate().DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void AddDomainEvent_MakesTheEventVisible()
    {
        var aggregate = NewAggregate();
        var raised = new TestDomainEvent("something happened");

        aggregate.Raise(raised);

        aggregate.DomainEvents.ShouldHaveSingleItem().ShouldBeSameAs(raised);
    }

    [Fact]
    public void AddDomainEvent_PreservesTheOrderInWhichEventsWereRaised()
    {
        var aggregate = NewAggregate();
        var first = new TestDomainEvent("first");
        var second = new TestDomainEvent("second");

        aggregate.Raise(first);
        aggregate.Raise(second);

        aggregate.DomainEvents.ShouldBe([first, second]);
    }

    [Fact]
    public void AddDomainEvents_AppendsTheWholeRange()
    {
        var aggregate = NewAggregate();
        var existing = new TestDomainEvent("existing");
        aggregate.Raise(existing);

        var batch = new IDomainEvent[] { new TestDomainEvent("a"), new OtherDomainEvent("b") };
        aggregate.RaiseMany(batch);

        aggregate.DomainEvents.Count.ShouldBe(3);
        aggregate.DomainEvents[0].ShouldBeSameAs(existing);
    }

    [Fact]
    public void ClearDomainEvents_EmptiesTheCollection()
    {
        var aggregate = NewAggregate();
        aggregate.Raise(new TestDomainEvent("something"));

        aggregate.ClearDomainEvents();

        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void RowVersion_IsEmptyBeforeTheAggregateIsPersisted()
    {
        NewAggregate().RowVersion.ShouldBeEmpty();
    }

    [Fact]
    public void RowVersion_IsExposedAsAReadOnlyViewThroughTheInterface()
    {
        IAggregateRoot aggregate = NewAggregate();

        aggregate.RowVersion.ShouldBeEmpty();
        typeof(IAggregateRoot).GetProperty(nameof(IAggregateRoot.RowVersion))!.CanWrite.ShouldBeFalse();
    }

    [Fact]
    public void RowVersion_ExposesNoPublicSetter()
    {
        var property = typeof(TestAggregate).GetProperty(nameof(TestAggregate.RowVersion));

        property.ShouldNotBeNull();
        property.GetSetMethod(nonPublic: false).ShouldBeNull();
    }

    [Fact]
    public void AnAggregate_IsAlsoAnEntity_AndKeepsItsIdentitySemantics()
    {
        var id = TestEntityId.Generate();

        new TestAggregate(id).Equals(new TestAggregate(id)).ShouldBeTrue();
    }
}
