using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Domain.Holds;

/// <summary>
/// Makes two hold queues into one, when Catalog reports that their editions were one record.
/// </summary>
/// <remarks>
/// A domain service and not a method on either queue, because the rules are about the pair and
/// belong to neither alone — and because they need no store once both queues are loaded, which is
/// the boundary the convention draws. The caller can name what to load, so the caller loads.
/// </remarks>
public interface IHoldQueueMergeDomainService
{
    /// <summary>
    /// Moves every claim of the absorbed queue into the surviving one, resolving what the union
    /// leaves duplicated.
    /// </summary>
    /// <param name="absorbed">The queue of the record that stopped answering.</param>
    /// <param name="surviving">The queue that goes on serving.</param>
    /// <returns>Success, or the reason the two could not be joined.</returns>
    Result Merge(HoldQueue absorbed, HoldQueue surviving);
}
