namespace LibraryManagement.Catalog.Domain.Authors;

/// <summary>
/// Stores and retrieves <see cref="Author"/> aggregates.
/// </summary>
/// <remarks>
/// <para>
/// Declared by the domain and implemented by the infrastructure: the domain says what it needs of a
/// store, and knows nothing of how one works.
/// </para>
/// <para>
/// There is no <c>SaveAsync</c>. A command is the unit of consistency, and a pipeline behavior writes
/// everything it changed through the module's unit of work once the handler has reported success. A
/// repository that saved on its own would let half a command survive the other half failing.
/// </para>
/// </remarks>
public interface IAuthorRepository
{
    /// <summary>
    /// Gets an author by identity.
    /// </summary>
    /// <param name="id">The author to get.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The author, or <see langword="null"/> when none is catalogued under that identifier.</returns>
    ValueTask<Author?> GetByIdAsync(AuthorId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether an author is catalogued under an identifier.
    /// </summary>
    /// <param name="id">The author to look for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the author exists; <see langword="false"/> otherwise.</returns>
    /// <remarks>
    /// Distinct from <see cref="GetByIdAsync"/> on purpose: crediting a work to its authors needs to
    /// know they exist, and nothing more. Loading each aggregate to answer that would pull a record
    /// and its variant names across for a question a single index answers.
    /// </remarks>
    ValueTask<bool> ExistsAsync(AuthorId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a newly registered author.
    /// </summary>
    /// <param name="author">The author to add.</param>
    /// <remarks>
    /// Synchronous, and deliberately so. Nothing is written here — the module's unit of work writes
    /// once the command has succeeded — and nothing has to be fetched either: identifiers are
    /// made by the domain before anything is stored. Entity Framework's asynchronous <c>AddAsync</c>
    /// exists for the one case that is not true of us, a value generator that must reach the
    /// database to produce a key. Returning a <see cref="ValueTask"/> here would promise input and
    /// output that never happens, and every caller would await it for nothing.
    /// </remarks>
    void Add(Author author);
}
