using LibraryManagement.Shared.Domain.Tests.TestDoubles;

namespace LibraryManagement.Shared.Domain.Tests;

public sealed class EntityIdTests
{
    [Fact]
    public void Create_WithAValue_RoundTripsThatValue()
    {
        var value = Guid.CreateVersion7();

        TestEntityId.Create(value).Value.ShouldBe(value);
    }

    [Fact]
    public void Create_WithTheEmptyGuid_Throws()
    {
        var act = () => TestEntityId.Create(Guid.Empty);

        act.ShouldThrow<ArgumentException>().ParamName.ShouldBe("value");
    }

    [Fact]
    public void Generate_ProducesAVersion7Uuid()
    {
        var id = TestEntityId.Generate();

        var version = id.Value.ToByteArray(bigEndian: true)[6] >> 4;
        version.ShouldBe(7);
    }

    [Fact]
    public void Generate_ProducesADistinctValueEveryTime()
    {
        var ids = Enumerable.Range(0, 500).Select(_ => TestEntityId.Generate().Value).ToList();

        ids.Distinct().Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void Equals_WithTheSameValue_IsTrueAndHashesMatch()
    {
        var value = Guid.CreateVersion7();
        var left = TestEntityId.Create(value);
        var right = TestEntityId.Create(value);

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void Equals_WithADifferentValue_IsFalse()
    {
        TestEntityId.Generate().Equals(TestEntityId.Generate()).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNull_IsFalse()
    {
        TestEntityId.Generate().Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Equals_AcrossIdentifierTypesSharingAValue_IsFalse()
    {
        var value = Guid.CreateVersion7();
        var testId = TestEntityId.Create(value);
        object otherId = OtherEntityId.Create(value);

        testId.Equals(otherId).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_HandlesNullsOnEitherSide()
    {
        var id = TestEntityId.Generate();
        TestEntityId? nothing = null;

        (nothing == null).ShouldBeTrue();
        (id == nothing).ShouldBeFalse();
        (nothing == id).ShouldBeFalse();
        (id != nothing).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_ComparesByValue()
    {
        var value = Guid.CreateVersion7();

        (TestEntityId.Create(value) == TestEntityId.Create(value)).ShouldBeTrue();
        (TestEntityId.Generate() != TestEntityId.Generate()).ShouldBeTrue();
    }

    [Fact]
    public void UsedAsADictionaryKey_IsFoundByAnEquivalentInstance()
    {
        var value = Guid.CreateVersion7();
        var map = new Dictionary<TestEntityId, string> { [TestEntityId.Create(value)] = "found" };

        map[TestEntityId.Create(value)].ShouldBe("found");
    }

    [Fact]
    public void UsedInAHashSet_IsFoundByAnEquivalentInstance()
    {
        var value = Guid.CreateVersion7();
        var set = new HashSet<TestEntityId> { TestEntityId.Create(value) };

        set.Contains(TestEntityId.Create(value)).ShouldBeTrue();
    }

    [Fact]
    public void ImplementsIEquatableOfItsOwnType_SoGenericCollectionsUseTheTypedComparer()
    {
        typeof(IEquatable<TestEntityId>).IsAssignableFrom(typeof(TestEntityId)).ShouldBeTrue();
        EqualityComparer<TestEntityId>.Default.GetType().Name.ShouldStartWith("GenericEqualityComparer");
    }

    [Fact]
    public void ToString_ReturnsTheUnderlyingValue()
    {
        var value = Guid.CreateVersion7();

        TestEntityId.Create(value).ToString().ShouldBe(value.ToString());
    }
}
