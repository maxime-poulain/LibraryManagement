using LibraryManagement.Shared.Application.CQS;

namespace LibraryManagement.Catalog.Application.Works.GetWorkById;

/// <summary>
/// Reads one work by identity.
/// </summary>
/// <param name="WorkId">The work to read.</param>
public sealed record GetWorkByIdQuery(Guid WorkId) : IQuery<WorkDetailsDto>;
