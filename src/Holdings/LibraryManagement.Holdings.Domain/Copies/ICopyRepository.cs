namespace LibraryManagement.Holdings.Domain.Copies;

/// <summary>
/// Stores and retrieves <see cref="Copy"/> aggregates.
/// </summary>
/// <remarks>
/// <para>
/// Declared by the domain and implemented by the infrastructure: the domain says what it needs of a
/// store, and knows nothing of how one works.
/// </para>
/// <para>
/// There is no <c>SaveAsync</c>. A command is the unit of consistency, and a pipeline behavior writes
/// everything it changed through the module's unit of work once the handler has reported success.
/// </para>
/// </remarks>
public interface ICopyRepository
{
    /// <summary>
    /// Gets a copy by identity.
    /// </summary>
    /// <param name="id">The copy to get.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The copy, or <see langword="null"/> when none is held under that identifier.</returns>
    ValueTask<Copy?> GetByIdAsync(CopyId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every copy filed under an edition, whatever state each is in.
    /// </summary>
    /// <param name="editionId">The edition to collect the copies of.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The copies, empty when the library holds none of that edition.</returns>
    /// <remarks>
    /// Withdrawn and lost copies included, deliberately: the one caller is the merge of two catalog
    /// records, and a copy that left the collection still records which edition it was a copy of.
    /// Filtering here would leave exactly those rows pointing at a record that stopped answering.
    /// </remarks>
    ValueTask<IReadOnlyList<Copy>> OfEditionAsync(
        EditionId editionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a barcode is already on a copy.
    /// </summary>
    /// <param name="barcode">The label to look for.</param>
    /// <param name="except">
    /// A copy to disregard, so that relabelling one to the label it already carries is not reported
    /// as a collision with itself.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the barcode is taken; <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// A barcode identifies one copy in the library, and a copy cannot see the other copies. The
    /// unique index is what holds the rule; this question exists so the refusal can name the label
    /// instead of surfacing as a constraint violation nobody can act on.
    /// </remarks>
    ValueTask<bool> BarcodeIsTakenAsync(
        Barcode barcode,
        CopyId? except = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a newly acquired copy.
    /// </summary>
    /// <param name="copy">The copy to add.</param>
    /// <remarks>
    /// Synchronous, and deliberately so: nothing is written here — the module's unit of work writes
    /// once the command has succeeded — and identifiers are made by the domain before anything is
    /// stored, so nothing has to be fetched either.
    /// </remarks>
    void Add(Copy copy);
}
