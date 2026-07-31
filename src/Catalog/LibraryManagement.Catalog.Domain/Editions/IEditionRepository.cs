namespace LibraryManagement.Catalog.Domain.Editions;

/// <summary>
/// Stores and retrieves <see cref="Edition"/> aggregates.
/// </summary>
/// <remarks>
/// There is no <c>SaveAsync</c>, for the reason given on
/// <see cref="Authors.IAuthorRepository"/>: a command is the unit of consistency, and the module's
/// unit of work writes everything it changed once the command has succeeded.
/// </remarks>
public interface IEditionRepository
{
    /// <summary>
    /// Gets an edition by identity.
    /// </summary>
    /// <param name="id">The edition to get.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The edition, or <see langword="null"/> when none is cataloged under that identifier.</returns>
    ValueTask<Edition?> GetByIdAsync(EditionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a newly cataloged edition.
    /// </summary>
    /// <param name="edition">The edition to add.</param>
    /// <remarks>
    /// Synchronous, for the reason given on <see cref="Authors.IAuthorRepository.Add"/>.
    /// </remarks>
    void Add(Edition edition);
}
