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

        // The reminders come with the loan. They are what the scheduled process reads to know what
        // it has already said, and a loan loaded without them would announce everything twice.
        return await context.Loans
            .Include(loan => loan.RemindersSent)
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
            .Include(loan => loan.RemindersSent)
            .SingleOrDefaultAsync(
                loan => loan.CopyId == copyId && loan.Status == LoanStatus.Active,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Loan>> ActiveDueBetweenAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await context.Loans
            .Include(loan => loan.RemindersSent)
            .Where(loan => loan.Status == LoanStatus.Active
                && loan.DueDate >= from
                && loan.DueDate <= to)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<Loan>> ActiveOverdueAsync(
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        return await context.Loans
            .Include(loan => loan.RemindersSent)
            .Where(loan => loan.Status == LoanStatus.Active && loan.DueDate < today)
            .ToListAsync(cancellationToken)
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
    /// <remarks>
    /// Tracked, unlike a read model's query: the caller repoints every loan it gets back, and the
    /// module's unit of work writes them once the command has succeeded.
    /// </remarks>
    public async ValueTask<IReadOnlyList<Loan>> ActiveOfEditionAsync(
        EditionId editionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(editionId);

        return await context.Loans
            .Where(loan => loan.EditionId == editionId && loan.Status == LoanStatus.Active)
            .ToListAsync(cancellationToken)
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

    /// <inheritdoc/>
    public async ValueTask<Loan?> MostRecentlyDeclaredLostForCopyAsync(
        CopyId copyId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(copyId);

        return await context.Loans
            .Where(loan => loan.CopyId == copyId && loan.Status == LoanStatus.DeclaredLost)
            .OrderByDescending(loan => loan.CheckedOutOn)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
