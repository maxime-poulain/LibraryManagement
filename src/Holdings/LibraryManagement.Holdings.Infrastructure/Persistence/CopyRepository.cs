using LibraryManagement.Holdings.Domain.Copies;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Holdings.Infrastructure.Persistence;

/// <summary>
/// Implements <see cref="ICopyRepository"/> over <see cref="HoldingsDbContext"/>.
/// </summary>
/// <param name="context">The module's store.</param>
public sealed class CopyRepository(HoldingsDbContext context) : ICopyRepository
{
    /// <inheritdoc/>
    public async ValueTask<Copy?> GetByIdAsync(
        CopyId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        // No Include: a copy owns nothing. Everything it holds is a value on the row itself, which
        // is what a physical object modelled honestly looks like.
        return await context.Copies
            .FirstOrDefaultAsync(copy => copy.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> BarcodeIsTakenAsync(
        Barcode barcode,
        CopyId? except = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(barcode);

        var candidates = context.Copies.Where(copy => copy.Barcode == barcode);

        if (except is not null)
        {
            candidates = candidates.Where(copy => copy.Id != except);
        }

        return await candidates.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>Add</c> and not <c>DbSet.AddAsync</c>: that overload exists for value generators which must
    /// reach the database to produce a key, and it would open a round trip with nothing to fetch.
    /// </remarks>
    public void Add(Copy copy)
    {
        ArgumentNullException.ThrowIfNull(copy);

        // Tracked, not written. The module's unit of work writes once the command has succeeded.
        context.Copies.Add(copy);
    }
}
