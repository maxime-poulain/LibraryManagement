using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Domain.Tests;

/// <summary>
/// The stock these tests work from. Named for what a library calls it, so a test reads as a
/// sentence about copies rather than as a sequence of constructions.
/// </summary>
internal static class Collection
{
    internal static Barcode ABarcode(string value = "30124000512") =>
        Barcode.Create(value).Match(barcode => barcode, _ => throw new InvalidOperationException(value));

    internal static Shelfmark AShelfmark(string value = "843.912 SAI") =>
        Shelfmark.Create(value).Match(mark => mark, _ => throw new InvalidOperationException(value));

    internal static DateOnly ADate => new(2024, 3, 14);

    /// <summary>A copy in service, which is what most of a collection is.</summary>
    internal static Copy ACopy(
        Barcode? barcode = null,
        Shelfmark? shelfmark = null,
        CopyCondition condition = CopyCondition.Good,
        bool referenceOnly = false)
        => Copy.Acquire(
            CopyId.Generate(),
            EditionId.Generate(),
            barcode ?? ABarcode(),
            shelfmark ?? AShelfmark(),
            condition,
            ADate,
            referenceOnly);

    /// <summary>Clears what acquiring raised, so a test asserts only on what it caused itself.</summary>
    internal static Copy Settled(this Copy copy)
    {
        copy.ClearDomainEvents();
        return copy;
    }

    internal static T Event<T>(this Copy copy) => copy.DomainEvents.OfType<T>().Single();
}
