using LibraryManagement.Circulation.Domain;
using LibraryManagement.Circulation.Domain.Loans;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Circulation.Infrastructure.Persistence;

/// <summary>
/// Implements <see cref="ILoanRepository"/> over <see cref="CirculationDbContext"/>.
/// </summary>
/// <param name="context">The module's store.</param>
public sealed class LoanRepository(CirculationDbContext context) : ILoanRepository
{
    /// <inheritdoc/>
    public async ValueTask<Loan?> GetByIdAsync(
        LoanId id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        // No Include: a loan owns nothing. Everything it holds is a value on the row itself.
        return await context.Loans
            .FirstOrDefaultAsync(loan => loan.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<Loan?> ActiveLoanForCopyAsync(
        CopyId copyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(copyId);

        // SingleOrDefault, not First: the filtered unique index promises at most one, and a
        // second would be corruption worth throwing over rather than silently picking from.
        return await context.Loans
            .SingleOrDefaultAsync(
                loan => loan.CopyId == copyId && loan.Status == LoanStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<int> CountActiveForBorrowerAsync(
        BorrowerId borrowerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(borrowerId);

        return await context.Loans
            .CountAsync(
                loan => loan.BorrowerId == borrowerId && loan.Status == LoanStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> BorrowerHasActiveLoanForEditionAsync(
        BorrowerId borrowerId,
        EditionId editionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(borrowerId);
        ArgumentNullException.ThrowIfNull(editionId);

        return await context.Loans
            .AnyAsync(
                loan => loan.BorrowerId == borrowerId
                    && loan.EditionId == editionId
                    && loan.Status == LoanStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CopyId>> OnActiveLoanAmongAsync(
        IReadOnlyCollection<CopyId> copyIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(copyIds);

        if (copyIds.Count == 0)
        {
            return [];
        }

        return await context.Loans
            .Where(loan => loan.Status == LoanStatus.Active && copyIds.Contains(loan.CopyId))
            .Select(loan => loan.CopyId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <c>Add</c> and not <c>DbSet.AddAsync</c>: that overload exists for value generators which
    /// must reach the database to produce a key, and it would open a round trip with nothing to
    /// fetch.
    /// </remarks>
    public void Add(Loan loan)
    {
        ArgumentNullException.ThrowIfNull(loan);

        // Tracked, not written. The module's unit of work writes once the command has succeeded.
        context.Loans.Add(loan);
    }
}
