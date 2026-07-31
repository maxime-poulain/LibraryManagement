using LibraryManagement.Catalog.PublishedLanguage;
using LibraryManagement.Holdings.Domain.Copies;

namespace LibraryManagement.Holdings.Application.Tests.TestDoubles;

// Nothing is saved here, which is exactly right: a handler never writes. It hands work to the store,
// and the module's unit of work writes once the command has succeeded.
internal sealed class InMemoryCopyRepository : ICopyRepository
{
    private readonly Dictionary<CopyId, Copy> _copies = [];

    public IReadOnlyCollection<Copy> Added => _copies.Values;

    public InMemoryCopyRepository With(params Copy[] copies)
    {
        foreach (var copy in copies)
        {
            _copies[copy.Id] = copy;
        }

        return this;
    }

    public ValueTask<Copy?> GetByIdAsync(CopyId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_copies.GetValueOrDefault(id));

    public ValueTask<bool> BarcodeIsTakenAsync(
        Barcode barcode,
        CopyId? except = null,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(
            _copies.Values.Any(copy => copy.Barcode == barcode && copy.Id != except));

    public void Add(Copy copy) => _copies[copy.Id] = copy;
}

// Stands in for Catalog across the boundary. That a stub of four lines is enough is the measure of
// how little crosses it: an identifier goes out and a yes or no comes back, which is why the edge
// needs no anticorruption layer today.
internal sealed class StubEditionCatalog(params Guid[] cataloged) : IEditionCatalog
{
    public ValueTask<bool> ExistsAsync(Guid editionId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(cataloged.Contains(editionId));
}
