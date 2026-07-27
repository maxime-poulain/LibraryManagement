namespace LibraryManagement.Shared.Domain.Tests.TestDoubles;

// A single-component value object.
public sealed class TestValueObject : ValueObject<TestValueObject>
{
    public TestValueObject(string code) => Code = code;

    public string Code { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}

// Structurally identical to <see cref="TestValueObject"/>, to prove equality is type-aware.
public sealed class OtherValueObject : ValueObject<OtherValueObject>
{
    public OtherValueObject(string code) => Code = code;

    public string Code { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }
}

// Several components, including a nullable one and a nested value object.
public sealed class MultiComponentValueObject : ValueObject<MultiComponentValueObject>
{
    public MultiComponentValueObject(string first, string? second, TestValueObject? nested)
    {
        First = first;
        Second = second;
        Nested = nested;
    }

    public string First { get; }

    public string? Second { get; }

    public TestValueObject? Nested { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return First;
        yield return Second;
        yield return Nested;
    }
}

// Yields a collection as a single component — the documented trap, pinned by a test.
public sealed class CollectionComponentValueObject : ValueObject<CollectionComponentValueObject>
{
    public CollectionComponentValueObject(IReadOnlyList<string> tags) => Tags = tags;

    public IReadOnlyList<string> Tags { get; }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Tags;
    }
}
