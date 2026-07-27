using LibraryManagement.Shared.Domain.Tests.TestDoubles;

namespace LibraryManagement.Shared.Domain.Tests;

public sealed class EntityTests
{
    [Fact]
    public void Id_IsTheOneSuppliedToTheConstructor()
    {
        var id = TestEntityId.Generate();

        new TestEntity(id).Id.ShouldBeSameAs(id);
    }

    [Fact]
    public void Equals_ComparesIdentityOnly_NotAttributes()
    {
        var id = TestEntityId.Generate();
        var left = new TestEntity(id) { Label = "one" };
        var right = new TestEntity(id) { Label = "completely different" };

        left.Equals(right).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithADifferentIdentity_IsFalse()
    {
        new TestEntity(TestEntityId.Generate())
            .Equals(new TestEntity(TestEntityId.Generate()))
            .ShouldBeFalse();
    }

    [Fact]
    public void Equals_AcrossEntityTypesSharingAnIdentity_IsFalse()
    {
        var id = TestEntityId.Generate();
        object other = new OtherEntity(id);

        new TestEntity(id).Equals(other).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithNull_IsFalse()
    {
        new TestEntity(TestEntityId.Generate()).Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void EqualityOperator_HandlesNullsOnEitherSide()
    {
        var entity = new TestEntity(TestEntityId.Generate());
        TestEntity? nothing = null;

        (nothing == null).ShouldBeTrue();
        (entity == nothing).ShouldBeFalse();
        (nothing == entity).ShouldBeFalse();
    }

    [Fact]
    public void InequalityOperator_HandlesNullsOnEitherSide()
    {
        // Both operands are nullable, matching the equality operator. Declaring them non-nullable
        // would make the ordinary `entity != null` a nullability warning while `entity == null`
        // stayed silent, for two operators that answer the same question.
        var entity = new TestEntity(TestEntityId.Generate());
        TestEntity? nothing = null;

        (nothing != null).ShouldBeFalse();
        (entity != nothing).ShouldBeTrue();
        (nothing != entity).ShouldBeTrue();
    }

    // --- Regression: Equals/GetHashCode contract -------------------------------------------------
    // GetHashCode used to return the reference hash while the entity had not been persisted yet,
    // even though Equals always compared by Id. Two equal entities could therefore hash
    // differently, and an entity's hash changed the moment it was saved — losing it from any
    // hash-based collection it had been placed in beforehand.

    [Fact]
    public void GetHashCode_ForTwoEqualEntities_IsTheSame()
    {
        var id = TestEntityId.Generate();

        new TestEntity(id).GetHashCode().ShouldBe(new TestEntity(id).GetHashCode());
    }

    [Fact]
    public void GetHashCode_IsTheHashOfTheIdentityAndNothingElse()
    {
        var id = TestEntityId.Generate();

        new TestEntity(id).GetHashCode().ShouldBe(id.GetHashCode());
    }

    [Fact]
    public void Entity_CarriesNoPersistenceStateOfItsOwn()
    {
        // "Has this been saved yet" is a question for the persistence layer, not a field on the
        // model. It used to live here as an _isTransient flag poked by an EF interceptor through
        // compiled expression trees, keyed on the field name as a string.
        // Auto-properties have compiler-generated backing fields; a hand-written field is what a
        // reflection-driven interceptor would need to reach, and there is none left.
        typeof(Entity<TestEntityId>)
            .GetFields(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.DeclaredOnly)
            .ShouldAllBe(field => field.Name.EndsWith("k__BackingField", StringComparison.Ordinal));
    }

    [Fact]
    public void HashSet_FindsAnEqualEntityThatIsADifferentInstance()
    {
        var id = TestEntityId.Generate();
        var set = new HashSet<TestEntity> { new(id) };

        set.Contains(new TestEntity(id)).ShouldBeTrue();
    }

    [Fact]
    public void Dictionary_FindsAnEqualEntityThatIsADifferentInstance()
    {
        var id = TestEntityId.Generate();
        var map = new Dictionary<TestEntity, string> { [new TestEntity(id)] = "found" };

        map[new TestEntity(id)].ShouldBe("found");
    }

    // --- Regression: Id cannot be reassigned -----------------------------------------------------
    // Id used to be `{ get; init; }`, which an object initializer could reach:
    // `new TestEntity(realId) { Id = somethingElse }` compiled and silently won over the
    // constructor. That no longer compiles, so this is asserted at the type level.

    [Fact]
    public void Id_ExposesNoPublicSetter()
    {
        var property = typeof(TestEntity).GetProperty(nameof(TestEntity.Id));

        property.ShouldNotBeNull();
        property.GetSetMethod(nonPublic: false).ShouldBeNull();
    }

    [Fact]
    public void Id_ExposesAPrivateSetterForThePersistenceLayer()
    {
        var property = typeof(Entity<TestEntityId>).GetProperty(
            nameof(Entity<TestEntityId>.Id),
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.DeclaredOnly);

        property.ShouldNotBeNull();
        property.GetSetMethod(nonPublic: true)!.IsPrivate.ShouldBeTrue();
    }
}
