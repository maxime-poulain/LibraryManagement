using LibraryManagement.Holdings.PublishedLanguage;

namespace LibraryManagement.Composition.Tests;

/// <summary>
/// Stands in for Holdings in compositions that do not register it: every edition can still serve,
/// no copy answers to a scan, nothing is on a shelf.
/// </summary>
/// <remarks>
/// The <c>NoChargesYet</c> pattern applied to the other port this module's daily process consults:
/// a host that composes Circulation without Holdings still owes the container an answer to *can
/// this edition still serve anyone*, and the honest neutral answer is yes — a stand-in that said
/// no would have the unfulfillability sweep cancel every queue in the store. In a composition
/// that registers <c>AddHoldingsModule</c>, its registration replaces this line and nothing else
/// moves.
/// </remarks>
internal sealed class EveryEditionStillServes : ICopyLendability
{
    public ValueTask<LendabilityAnswer> OfAsync(
        Guid copyId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new LendabilityAnswer(Lendability.NoSuchCopy, null));

    public ValueTask<IReadOnlyList<Guid>> LendableCopiesOfAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<Guid>>([]);

    public ValueTask<bool> AnyCopyExpectedToServeAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(true);
}
