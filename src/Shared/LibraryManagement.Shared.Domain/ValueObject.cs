namespace LibraryManagement.Shared.Domain;

/// <summary>
/// A Value Object (VO) is a Domain Object that represents a concept based on
/// its attributes and carries no concept of identity.
/// It should be immutable and its equality is defined by the equality
/// of its attributes.
/// VOs are usually used as a property of an Entity or another VO, and can be shared
/// between different Entities.
/// They model object attributes or characteristics that are transient
/// and do not have identity.
/// </summary>
/// <remarks>
/// <para>
/// According to Eric Evans in "Domain-Driven Design", "A VALUE OBJECT is an object
/// that describes some characteristic or attribute but carries no concept of identity.
/// Value objects are distinguished by the fact that two value objects with
/// the same attributes are interchangeable, no matter where or when they were created."
/// </para>
/// <para>
/// Vaughn Vernon in "Implementing Domain-Driven Design" explains that "Value Objects
/// are an important DDD concept. They model object attributes or characteristics that
/// are transient and do not have identity. They are usually small and immutable, and can be used for comparisons."
/// </para>
/// <para>
/// Scott Millett and Nick Tune in "Patterns, Principles, and Practices of Domain-Driven Design" state that
/// "Value Objects represent some concept or idea, where the concept is defined by the state of its properties.
/// Two Value Objects are considered equal if they have the same properties. They are typically immutable, so they cannot be changed once created."
/// </para>
/// <para>
/// This type deliberately does not implement <see cref="IComparable"/>: not every value object
/// has a natural order, and a base class cannot define one that is meaningful for all of them.
/// A value object that is genuinely orderable declares <see cref="IComparable{T}"/> against its
/// own type, where the ordering can be defined correctly and explicitly.
/// </para>
/// </remarks>
public abstract class ValueObject
{
    /// <summary>
    /// Returns the components that define this value object. Two value objects of the same
    /// concrete type are equal when their components are pairwise equal, in order.
    /// </summary>
    /// <remarks>
    /// Components are compared with <see cref="EqualityComparer{T}.Default"/>, so yield scalars
    /// or nested value objects. A collection yielded as a single component is compared by
    /// reference and not by content: project it to a stable scalar instead.
    /// </remarks>
    /// <returns>The components that make up this value object.</returns>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <summary>
    /// Determines whether this value object and <paramref name="other"/> have the same concrete
    /// type and equal components. This is the single comparison used by every equality member
    /// of this type and of <see cref="ValueObject{TSelf}"/>.
    /// </summary>
    /// <param name="other">The value object to compare with this one.</param>
    /// <returns>
    /// <see langword="true"/> if both value objects are equal; <see langword="false"/> otherwise.
    /// </returns>
    protected bool EqualsCore(ValueObject? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <inheritdoc/>
    public sealed override bool Equals(object? obj)
    {
        return obj is ValueObject other && EqualsCore(other);
    }

    /// <inheritdoc/>
    public sealed override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Determines whether two value objects are equal.
    /// </summary>
    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Determines whether two value objects are different.
    /// </summary>
    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// A <see cref="ValueObject"/> that is equatable against its own concrete type.
/// Derive from this rather than from <see cref="ValueObject"/>: it lets generic collections
/// such as <see cref="Dictionary{TKey,TValue}"/> and <see cref="HashSet{T}"/> resolve a typed
/// comparer instead of falling back to <see cref="object.Equals(object?)"/>.
/// </summary>
/// <typeparam name="TSelf">The concrete value object type deriving from this class.</typeparam>
/// <remarks>
/// This mirrors the <see cref="Entity"/> / <see cref="Entity{TEntityId}"/> pairing: the
/// non-generic type remains available as a marker for infrastructure, while the generic one
/// carries the typed contract.
/// </remarks>
public abstract class ValueObject<TSelf> : ValueObject, IEquatable<TSelf>
    where TSelf : ValueObject<TSelf>
{
    /// <inheritdoc cref="ValueObject.EqualsCore"/>
    public bool Equals(TSelf? other)
    {
        return EqualsCore(other);
    }
}
