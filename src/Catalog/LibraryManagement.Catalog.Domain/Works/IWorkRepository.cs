namespace LibraryManagement.Catalog.Domain.Works;

/// <summary>
/// Stores and retrieves <see cref="Work"/> aggregates.
/// </summary>
/// <remarks>
/// There is no <c>SaveAsync</c>, for the reason given on
/// <see cref="Authors.IAuthorRepository"/>: a command is the unit of consistency, and the module's
/// unit of work writes everything it changed once the command has succeeded.
/// </remarks>
public interface IWorkRepository
{
    /// <summary>
    /// Gets a work by identity.
    /// </summary>
    /// <param name="id">The work to get.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The work, or <see langword="null"/> when none is catalogued under that identifier.</returns>
    ValueTask<Work?> GetByIdAsync(WorkId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a newly catalogued work.
    /// </summary>
    /// <param name="work">The work to add.</param>
    /// <remarks>
    /// Synchronous, for the reason given on <see cref="Authors.IAuthorRepository.Add"/>.
    /// </remarks>
    void Add(Work work);
}
