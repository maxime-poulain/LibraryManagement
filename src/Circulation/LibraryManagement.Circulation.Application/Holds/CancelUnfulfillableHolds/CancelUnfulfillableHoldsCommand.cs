using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Holds.CancelUnfulfillableHolds;

/// <summary>
/// Sweeps the queues whose editions have no copy left to serve them with, and ends every claim in
/// them — out loud.
/// </summary>
/// <remarks>
/// A claim is a promise of the next available copy, and an edition whose last copy was withdrawn,
/// lost or shut away — with nothing expected back from repair — has no next to promise. Left
/// alone, such a queue survives its edition: eleven people wait forever for a manga nobody can
/// buy back this year, each with one of their five places silently gone. Daily rather than
/// event-driven, deliberately: whether an edition can still serve is a question about all its
/// copies at once, which the port answers live, and a day's lag on ending a promise measured in
/// months is nothing.
/// </remarks>
public sealed record CancelUnfulfillableHoldsCommand : ICommand<Result>;
