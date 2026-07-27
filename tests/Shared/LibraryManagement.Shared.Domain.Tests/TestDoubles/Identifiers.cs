namespace LibraryManagement.Shared.Domain.Tests.TestDoubles;

// A concrete identifier, declared the way a bounded context is expected to declare one.
public sealed class TestEntityId : EntityId<TestEntityId>, IEntityId<TestEntityId>
{
    private TestEntityId(Guid value) : base(value)
    {
    }

    static TestEntityId IEntityId<TestEntityId>.FromValue(Guid value) => new(value);
}

// A second identifier type, so that two identifiers wrapping the same Guid can be compared.
public sealed class OtherEntityId : EntityId<OtherEntityId>, IEntityId<OtherEntityId>
{
    private OtherEntityId(Guid value) : base(value)
    {
    }

    static OtherEntityId IEntityId<OtherEntityId>.FromValue(Guid value) => new(value);
}
