using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Holdings.Domain;

/// <summary>
/// Every <see cref="ErrorCode"/> the Holdings context can produce.
/// </summary>
/// <remarks>
/// Declared here rather than in the shared kernel, and prefixed with the context's own name, so no
/// two contexts can claim the same code and a code is self-describing wherever it surfaces.
/// </remarks>
public static class HoldingsErrorCodes
{
    /// <summary>A barcode is empty, blank, or outside the length a label may run to.</summary>
    public static readonly ErrorCode InvalidBarcode = new("Holdings.InvalidBarcode");

    /// <summary>A shelfmark is empty, blank, or longer than a shelfmark may be.</summary>
    public static readonly ErrorCode InvalidShelfmark = new("Holdings.InvalidShelfmark");

    /// <summary>The barcode is already on another copy. It identifies one copy in the library.</summary>
    public static readonly ErrorCode BarcodeAlreadyInUse = new("Holdings.BarcodeAlreadyInUse");

    /// <summary>No copy is held under that identifier.</summary>
    public static readonly ErrorCode CopyNotFound = new("Holdings.CopyNotFound");

    /// <summary>No edition is cataloged under that identifier, so nothing can be a copy of it.</summary>
    public static readonly ErrorCode EditionNotFound = new("Holdings.EditionNotFound");

    /// <summary>
    /// The copy has left the collection, and a withdrawal is not undone.
    /// </summary>
    public static readonly ErrorCode CopyIsWithdrawn = new("Holdings.CopyIsWithdrawn");

    /// <summary>
    /// The operation does not apply to the state the copy is in — repairing one already in repair,
    /// returning one that never left, finding one nobody lost.
    /// </summary>
    public static readonly ErrorCode StatusDoesNotAllowIt = new("Holdings.StatusDoesNotAllowIt");
}
