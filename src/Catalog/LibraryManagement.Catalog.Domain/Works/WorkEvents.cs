using LibraryManagement.Shared.Domain;

namespace LibraryManagement.Catalog.Domain.Works;

/// <summary>
/// A work was catalogued.
/// </summary>
/// <param name="WorkId">The new work.</param>
/// <param name="Title">Its title.</param>
public sealed record WorkRegistered(WorkId WorkId, Title Title) : DomainEvent;
