using LibraryManagement.Shared.Domain.Errors;

namespace LibraryManagement.Catalog.Domain;

/// <summary>
/// Every <see cref="ErrorCode"/> the Catalog context can produce.
/// </summary>
/// <remarks>
/// Declared here rather than in the shared kernel, and prefixed with the context's own name, so no
/// two contexts can claim the same code and a code is self-describing wherever it surfaces.
/// </remarks>
public static class CatalogErrorCodes
{
    /// <summary>A name is empty, blank, or longer than a name form may run to.</summary>
    public static readonly ErrorCode InvalidName = new("Catalog.InvalidName");

    /// <summary>A title is empty, blank, or longer than a title may be.</summary>
    public static readonly ErrorCode InvalidTitle = new("Catalog.InvalidTitle");

    /// <summary>Birth and death years that cannot both be true of one person.</summary>
    public static readonly ErrorCode InvalidLifeYears = new("Catalog.InvalidLifeYears");

    /// <summary>A value that is not an ISBN: wrong shape, wrong prefix, or a failed check digit.</summary>
    public static readonly ErrorCode InvalidIsbn = new("Catalog.InvalidIsbn");

    /// <summary>
    /// The name is already recorded for this author, as the preferred name or as a variant.
    /// </summary>
    public static readonly ErrorCode DuplicateName = new("Catalog.DuplicateName");

    /// <summary>No author is cataloged under that identifier.</summary>
    public static readonly ErrorCode AuthorNotFound = new("Catalog.AuthorNotFound");

    /// <summary>No work is cataloged under that identifier.</summary>
    public static readonly ErrorCode WorkNotFound = new("Catalog.WorkNotFound");

    /// <summary>The author is already credited on this work.</summary>
    public static readonly ErrorCode DuplicateAuthor = new("Catalog.DuplicateAuthor");
}
