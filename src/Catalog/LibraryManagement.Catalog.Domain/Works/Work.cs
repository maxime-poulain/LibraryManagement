using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Shared.Domain;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Catalog.Domain.Works;

/// <summary>
/// An intellectual creation, independent of any printing of it. <em>Le Petit Prince</em> is one work,
/// however many editions of it exist and whichever of them a library happens to own.
/// </summary>
/// <remarks>
/// <para>
/// A work holds the identifiers of its authors and nothing else about them. Names live in the
/// <see cref="Author"/> records, so a person filed under a new name is one change in one place, and
/// every work they wrote follows without being touched.
/// </para>
/// <para>
/// A work with no author at all is valid, and not an oversight: anonymous works, traditional tales
/// and many mediaeval texts have none. A model that demanded one would force a librarian to invent
/// an author for <em>Le Roman de Renart</em>.
/// </para>
/// <para>
/// The title is a <see cref="PreferredTitle"/> and not a <c>Title</c>, for the reason
/// <see cref="Author.PreferredName"/> is not an <c>AuthorizedName</c>: it is a cataloguer's choice
/// among the forms a work is known by, not a description of it. <em>Le Petit Prince</em>,
/// <em>The Little Prince</em> and <em>Der kleine Prinz</em> are one work, and one of the three is
/// filed under. The qualifier also keeps the word free for the day an edition carries the title
/// printed on its own title page, which is a different fact about a different thing.
/// </para>
/// </remarks>
public sealed class Work : AggregateRoot<WorkId>
{
    private readonly List<AuthorId> _authorIds = [];

    private Work(WorkId id, Title preferredTitle) : base(id) => PreferredTitle = preferredTitle;

    /// <summary>Gets the title the work is filed under — its preferred title.</summary>
    public Title PreferredTitle { get; private set; }

    /// <summary>Gets the authors credited with the work, in the order they were credited.</summary>
    public IReadOnlyList<AuthorId> AuthorIds => _authorIds.AsReadOnly();

    /// <summary>
    /// Catalogues a work.
    /// </summary>
    /// <param name="id">The identifier the work will keep for its whole life.</param>
    /// <param name="title">The title.</param>
    /// <param name="authorIds">The authors credited, which may be none.</param>
    /// <returns>The new work, or the reason it could not be catalogued.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> or <paramref name="title"/> is null.</exception>
    public static Result<Work> Register(WorkId id, Title title, IEnumerable<AuthorId>? authorIds = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(title);

        var work = new Work(id, title);

        foreach (var authorId in authorIds ?? [])
        {
            var refusal = work.CreditAuthor(authorId)
                .Match<Result<Work>?>(() => null, Result<Work>.Failure);

            if (refusal is not null)
            {
                return refusal;
            }
        }

        work.AddDomainEvent(new WorkRegistered(id, title));

        return Result<Work>.Success(work);
    }

    /// <summary>
    /// Records the work under a different title.
    /// </summary>
    /// <param name="title">The title from now on.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
    /// <remarks>
    /// Retitling to the current title records nothing: nothing happened, and an event saying
    /// otherwise would send the search projection to replace an access point with itself.
    /// </remarks>
    public void Retitle(Title title)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (title == PreferredTitle)
        {
            return;
        }

        var previous = PreferredTitle;
        PreferredTitle = title;

        AddDomainEvent(new WorkRetitled(Id, previous, title));
    }

    /// <summary>
    /// Credits an author with the work.
    /// </summary>
    /// <param name="authorId">The author to credit.</param>
    /// <returns>Success, or the reason the author was refused.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorId"/> is null.</exception>
    public Result CreditAuthor(AuthorId authorId)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        if (_authorIds.Contains(authorId))
        {
            return Result.Failure(
                CatalogErrorCodes.DuplicateAuthor,
                "That author is already credited with this work.");
        }

        _authorIds.Add(authorId);

        return Result.Success();
    }

    /// <summary>
    /// Removes an author's credit.
    /// </summary>
    /// <param name="authorId">The author to remove.</param>
    /// <returns>
    /// <see langword="true"/> when the author was credited and no longer is;
    /// <see langword="false"/> when they were not credited in the first place.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorId"/> is null.</exception>
    public bool RemoveAuthorCredit(AuthorId authorId)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        return _authorIds.Remove(authorId);
    }
}
