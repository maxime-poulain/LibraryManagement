using LibraryManagement.Shared.Domain.Tests.TestDoubles;

namespace LibraryManagement.Shared.Domain.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void Equals_WithIdenticalComponents_IsTrueAndHashesMatch()
    {
        var left = new TestValueObject("ISBN-13");
        var right = new TestValueObject("ISBN-13");

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentComponents_IsFalse()
    {
        new TestValueObject("one").Equals(new TestValueObject("two")).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNull_IsFalse()
    {
        new TestValueObject("code").Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_AcrossTypesWithIdenticalComponents_IsFalse()
    {
        object other = new OtherValueObject("same");

        new TestValueObject("same").Equals(other).ShouldBeFalse();
    }

    [Fact]
    public void Equals_IsSensitiveToComponentOrder()
    {
        var left = new MultiComponentValueObject("a", "b", null);
        var right = new MultiComponentValueObject("b", "a", null);

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    public void Equals_HandlesNullComponents()
    {
        var left = new MultiComponentValueObject("a", null, null);
        var right = new MultiComponentValueObject("a", null, null);

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_DistinguishesANullComponentFromAValuedOne()
    {
        var withNull = new MultiComponentValueObject("a", null, null);
        var withValue = new MultiComponentValueObject("a", "b", null);

        withNull.Equals(withValue).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ComparesNestedValueObjectsStructurally()
    {
        var left = new MultiComponentValueObject("a", "b", new TestValueObject("nested"));
        var right = new MultiComponentValueObject("a", "b", new TestValueObject("nested"));

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_WithADifferentNestedValueObject_IsFalse()
    {
        var left = new MultiComponentValueObject("a", "b", new TestValueObject("one"));
        var right = new MultiComponentValueObject("a", "b", new TestValueObject("two"));

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_HandlesNullsOnEitherSide()
    {
        var value = new TestValueObject("code");
        TestValueObject? nothing = null;

        (nothing == null).ShouldBeTrue();
        (value == nothing).ShouldBeFalse();
        (nothing == value).ShouldBeFalse();
        (value != nothing).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_ComparesStructurally()
    {
        (new TestValueObject("x") == new TestValueObject("x")).ShouldBeTrue();
        (new TestValueObject("x") != new TestValueObject("y")).ShouldBeTrue();
    }

    [Fact]
    public void UsedAsADictionaryKey_IsFoundByAnEquivalentInstance()
    {
        var map = new Dictionary<TestValueObject, string> { [new TestValueObject("k")] = "found" };

        map[new TestValueObject("k")].ShouldBe("found");
    }

    [Fact]
    public void UsedInAHashSet_IsFoundByAnEquivalentInstance()
    {
        var set = new HashSet<TestValueObject> { new("k") };

        set.Contains(new TestValueObject("k")).ShouldBeTrue();
    }

    [Fact]
    public void ImplementsIEquatableOfItsOwnType_SoGenericCollectionsUseTheTypedComparer()
    {
        typeof(IEquatable<TestValueObject>).IsAssignableFrom(typeof(TestValueObject)).ShouldBeTrue();
        EqualityComparer<TestValueObject>.Default.GetType().Name.ShouldStartWith("GenericEqualityComparer");
    }

    [Fact]
    public void DoesNotImplementIComparable_OrderingIsOptedIntoPerType()
    {
        typeof(IComparable).IsAssignableFrom(typeof(TestValueObject)).ShouldBeFalse();
        typeof(IComparable<TestValueObject>).IsAssignableFrom(typeof(TestValueObject)).ShouldBeFalse();
    }

    // Pins the trap documented on GetEqualityComponents: a component that is itself a collection is
    // compared with EqualityComparer<object>.Default, which for a list means reference equality.
    // Two value objects holding equal-but-distinct collections are therefore NOT equal. This test
    // exists so the behavior cannot drift silently — change it deliberately or not at all.
    [Fact]
    public void ACollectionComponent_IsComparedByReference_NotByContent()
    {
        var left = new CollectionComponentValueObject(["a", "b"]);
        var right = new CollectionComponentValueObject(["a", "b"]);

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    public void ACollectionComponent_SharedByReference_CompareEqual()
    {
        string[] shared = ["a", "b"];

        new CollectionComponentValueObject(shared)
            .Equals(new CollectionComponentValueObject(shared))
            .ShouldBeTrue();
    }
}
