namespace LibraryManagement.Shared.Domain.Tests.TestDoubles;

// A concrete entity carrying a mutable attribute, so that equality can be shown to ignore it.
public sealed class TestEntity : Entity<TestEntityId>
{
    public TestEntity(TestEntityId id) : base(id)
    {
    }

    public string Label { get; set; } = string.Empty;
}

// A second entity type over the *same* identifier type, to exercise the GetType() check.
public sealed class OtherEntity : Entity<TestEntityId>
{
    public OtherEntity(TestEntityId id) : base(id)
    {
    }
}

// A concrete aggregate root exposing the protected domain-event API.
public sealed class TestAggregate : AggregateRoot<TestEntityId>
{
    public TestAggregate(TestEntityId id) : base(id)
    {
    }

    public void Raise(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);

    public void RaiseMany(IEnumerable<IDomainEvent> domainEvents) => AddDomainEvents(domainEvents);
}
