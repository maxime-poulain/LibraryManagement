using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.ExpireUncollectedHolds;

/// <summary>
/// Ends the claims nobody came for, and moves each released copy on to whoever is next.
/// </summary>
/// <remarks>
/// The one moment of the scheduled process that makes a queue turn without anybody at the desk.
/// Idempotent by construction rather than by a flag: an expired claim leaves the queue, so a
/// second run finds nothing to expire.
/// </remarks>
public sealed record ExpireUncollectedHoldsCommand : ICommand<Result>;
