using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Holdings.Application.Copies.NoteCopyAccountedFor;

/// <summary>
/// Notes that the world has accounted for a copy — it passed over the desk in somebody's hands —
/// and finds it if this module still held it lost.
/// </summary>
/// <param name="CopyId">The copy in hand.</param>
/// <remarks>
/// The reaction to Circulation announcing a return. For almost every return it notes nothing: the
/// record already matches the shelf. It exists for the copy this module holds as unaccounted for
/// while a member is handing it back — declared lost at a stocktake's word or a desk's, then
/// returned as if nothing had happened — which stayed lost until a human happened to notice, on a
/// record that says <em>unaccounted for</em> about an object in hand.
/// </remarks>
public sealed record NoteCopyAccountedForCommand(Guid CopyId) : ICommand<Result>;
