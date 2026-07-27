using Mediator;

namespace LibraryManagement.Shared.Domain;

/// <summary>
/// <para>
/// IDomainEvent represents a state change within an Aggregate in a specific bounded context.
/// Domain Events encapsulate outcomes of domain operations and are essential elements of
/// Domain-Driven Design (DDD). They provide a mechanism to capture and communicate
/// important changes within a specific bounded context.
///</para>
///
/// <para>
/// In a modular architecture, where several bounded contexts exist, each bounded context
/// handles a specific domain area. For example, in a library system, a 'Lending' bounded
/// context could handle all functionality related to loans.
/// In such a scenario, a Loan entity might raise a 'LoanOverdue' Domain Event to signify
/// that a borrowed copy passed its due date. Components within the 'Lending' bounded context
/// can then react to the 'LoanOverdue' event as needed.
/// </para>
///
/// <para>
/// However, for communicating state changes across different bounded contexts, Integration Events
/// are utilized. These are special kinds of events that convey information meaningful to
/// multiple bounded contexts. For instance, a 'FinePaid' Integration Event from a 'Billing'
/// bounded context could trigger actions in a 'Lending' bounded context.
/// </para>
/// <para>
/// By employing Domain Events and Integration Events, modular architectures achieve improved
/// communication, modularity, and extensibility. It's crucial for developers to understand these
/// DDD concepts to design robust and maintainable systems.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// A domain event carries exactly two pieces of metadata: <see cref="EventId"/> and
/// <see cref="OccurredOn"/>. Everything else a message usually travels with — correlation and
/// causation identifiers, a logical type name, a payload schema version, a tenant — describes how
/// a message is <em>transported and stored</em>, not what happened in the domain. Those belong on
/// the envelope of an integration event, where the infrastructure stamps them. Putting them here
/// would force every aggregate to know about request tracing and serialization.
/// </para>
/// <para>
/// Derive from <see cref="DomainEvent"/> rather than implementing this interface directly, so the
/// metadata is supplied consistently.
/// </para>
/// </remarks>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Uniquely identifies this occurrence of the event.
    /// </summary>
    /// <remarks>
    /// Two events describing the same state change raised twice are two distinct occurrences with
    /// two distinct identifiers. This is what lets a handler recognise work it has already done —
    /// and what an outbox needs to guarantee an event is published once and only once.
    /// </remarks>
    Guid EventId { get; }

    /// <summary>
    /// The instant at which the event was raised.
    /// </summary>
    /// <remarks>
    /// This is metadata about the event, not a business value. When an instant carries domain
    /// meaning — the date a copy was returned, the date a reservation expires — put it in the
    /// event's own payload as a named, typed member. Handlers must not derive business rules from
    /// <see cref="OccurredOn"/>.
    /// </remarks>
    DateTimeOffset OccurredOn { get; }
}
