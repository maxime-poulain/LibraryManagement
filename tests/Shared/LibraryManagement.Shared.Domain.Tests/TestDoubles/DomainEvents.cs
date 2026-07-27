namespace LibraryManagement.Shared.Domain.Tests.TestDoubles;

// A concrete domain event. The business instant lives in the payload, deliberately separate from
// the OccurredOn metadata supplied by the base.
public sealed record TestDomainEvent(string Payload) : DomainEvent;

// A second event type carrying the same payload shape.
public sealed record OtherDomainEvent(string Payload) : DomainEvent;
