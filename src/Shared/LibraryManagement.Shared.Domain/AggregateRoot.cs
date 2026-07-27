using System.Diagnostics.CodeAnalysis;

namespace LibraryManagement.Shared.Domain;

/// <summary>
/// Represents the root of an aggregate, which is a group of related entities and value objects
/// that form a consistency boundary for business rules and transactions.
/// Inherits from Entity and implements IAggregateRoot.
///
/// Example: In an e-commerce domain, an Order (Aggregate Root) may consist of OrderLines (Entities)
/// and Address (Value Objects). The Order entity is responsible for maintaining consistency
/// and managing the state of the entire aggregate.
/// </summary>
/// <typeparam name="TEntityId">The type of the unique identifier for the aggregate root entity.</typeparam>
/// <remarks>
/// For more information, see:
/// <list type="bullet">
/// <item><description><see href="https://martinfowler.com/bliki/DDD_Aggregate.html">Martin Fowler's DDD Aggregate</see></description></item>
/// <item><description><see href="https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice">Microsoft's DDD-Oriented Microservice</see></description></item>
/// </list>
/// </remarks>
public abstract class AggregateRoot<TEntityId> : Entity<TEntityId>, IAggregateRoot
    where TEntityId : EntityId<TEntityId>, IEntityId<TEntityId>
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AggregateRoot{TEntityId}"/> class with the
    /// identity it will keep for its whole lifetime.
    /// </summary>
    /// <param name="id">The identifier of the aggregate root.</param>
    /// <remarks>
    /// See <see cref="Entity{TEntityId}"/> for why the identifier is supplied at construction
    /// rather than assigned on save.
    /// </remarks>
    protected AggregateRoot(TEntityId id) : base(id)
    {
    }

    /// <inheritdoc cref="IAggregateRoot.RowVersion"/>
    /// <remarks>
    /// Typed as an array rather than <see cref="IReadOnlyList{T}"/> because that is the shape the
    /// persistence layer maps a <c>rowversion</c> column to. Consumers should go through
    /// <see cref="IAggregateRoot.RowVersion"/>, which hands out a read-only view.
    /// </remarks>
    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "A SQL Server rowversion column maps to byte[]; the persistence layer requires this exact shape.")]
    public byte[] RowVersion { get; private set; } = [];

    /// <inheritdoc/>
    IReadOnlyList<byte> IAggregateRoot.RowVersion => RowVersion;

    /// <inheritdoc/>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to the aggregate root.
    /// </summary>
    /// <param name="domainEvent">The domain event to add.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Adds a collection of domain events to the aggregate root.
    /// </summary>
    /// <param name="domainEvents">The collection of domain events to add.</param>
    protected void AddDomainEvents(IEnumerable<IDomainEvent> domainEvents)
    {
        _domainEvents.AddRange(domainEvents);
    }

    /// <inheritdoc/>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Marker interface for <see cref="AggregateRoot{TEntityId}"/>, letting infrastructure discover
/// aggregate roots without knowing their identifier type.
/// </summary>
public interface IAggregateRoot : IHasDomainEvents
{
    /// <summary>
    /// The optimistic concurrency token of this aggregate. Empty until the aggregate has been
    /// persisted once.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It lives on the aggregate root and nowhere else, because the aggregate — not the individual
    /// entity — is the consistency boundary. Changing a child entity is a change to its aggregate,
    /// and must contend for the same token.
    /// </para>
    /// <para>
    /// The domain never reads or writes it. It exists so that two concurrent transactions touching
    /// the same aggregate cannot both win: the second one to reach the database finds the token it
    /// read no longer current, and fails. Concretely, two employees reserving the same copy at the
    /// same moment.
    /// </para>
    /// <para>
    /// A <see cref="byte"/> array is the shape SQL Server's <c>rowversion</c> column maps to, and
    /// the database maintains it on every update. On a provider without <c>rowversion</c> —
    /// PostgreSQL, whose equivalent is the <c>xmin</c> system column — the concurrency token has to
    /// be configured differently, and this property would change shape with it.
    /// </para>
    /// </remarks>
    IReadOnlyList<byte> RowVersion { get; }
}
