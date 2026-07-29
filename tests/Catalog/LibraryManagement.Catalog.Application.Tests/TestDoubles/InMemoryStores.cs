using LibraryManagement.Catalog.Domain.Authors;
using LibraryManagement.Catalog.Domain.Editions;
using LibraryManagement.Catalog.Domain.Works;

namespace LibraryManagement.Catalog.Application.Tests.TestDoubles;

// Nothing is saved here, which is exactly right: a handler never writes. It hands work to the store,
// and the module's unit of work writes once the command has succeeded.
internal sealed class InMemoryAuthorRepository : IAuthorRepository
{
    private readonly Dictionary<AuthorId, Author> _authors = [];

    public IReadOnlyCollection<Author> Added => _authors.Values;

    public InMemoryAuthorRepository With(params Author[] authors)
    {
        foreach (var author in authors)
        {
            _authors[author.Id] = author;
        }

        return this;
    }

    public ValueTask<Author?> GetByIdAsync(AuthorId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_authors.GetValueOrDefault(id));

    public ValueTask<bool> ExistsAsync(AuthorId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_authors.ContainsKey(id));

    public void Add(Author author) => _authors[author.Id] = author;
}

internal sealed class InMemoryWorkRepository : IWorkRepository
{
    private readonly Dictionary<WorkId, Work> _works = [];

    public IReadOnlyCollection<Work> Added => _works.Values;

    public InMemoryWorkRepository With(params Work[] works)
    {
        foreach (var work in works)
        {
            _works[work.Id] = work;
        }

        return this;
    }

    public ValueTask<Work?> GetByIdAsync(WorkId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_works.GetValueOrDefault(id));

    public ValueTask<bool> ExistsAsync(WorkId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_works.ContainsKey(id));

    public void Add(Work work) => _works[work.Id] = work;
}

internal sealed class InMemoryEditionRepository : IEditionRepository
{
    private readonly Dictionary<EditionId, Edition> _editions = [];

    public IReadOnlyCollection<Edition> Added => _editions.Values;

    public ValueTask<Edition?> GetByIdAsync(EditionId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_editions.GetValueOrDefault(id));

    public void Add(Edition edition) => _editions[edition.Id] = edition;
}
