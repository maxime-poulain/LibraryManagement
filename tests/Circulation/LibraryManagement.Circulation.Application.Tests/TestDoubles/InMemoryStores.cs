using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.PublishedLanguage;
using LibraryManagement.Holdings.PublishedLanguage;
using LibraryManagement.Members.PublishedLanguage;

namespace LibraryManagement.Circulation.Application.Tests.TestDoubles;

// Nothing is saved here, which is exactly right: a handler never writes. It hands work to the
// stores, and the module's unit of work writes once the command has succeeded — the return moment
// changing two aggregates in one save is exactly this arrangement.
internal sealed class InMemoryLoanRepository : ILoanRepository
{
    private readonly Dictionary<LoanId, Loan> _loans = [];

    public IReadOnlyCollection<Loan> Added => _loans.Values;

    public InMemoryLoanRepository With(params Loan[] loans)
    {
        foreach (var loan in loans)
        {
            _loans[loan.Id] = loan;
        }

        return this;
    }

    public ValueTask<Loan?> GetByIdAsync(LoanId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_loans.GetValueOrDefault(id));

    public ValueTask<Loan?> ActiveLoanForCopyAsync(
        CopyId copyId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_loans.Values.SingleOrDefault(
            loan => loan.CopyId == copyId && loan.Status == LoanStatus.Active));

    public ValueTask<int> CountActiveForBorrowerAsync(
        BorrowerId borrowerId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_loans.Values.Count(
            loan => loan.BorrowerId == borrowerId && loan.Status == LoanStatus.Active));

    public ValueTask<bool> BorrowerHasActiveLoanForEditionAsync(
        BorrowerId borrowerId,
        EditionId editionId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_loans.Values.Any(
            loan => loan.BorrowerId == borrowerId
                && loan.EditionId == editionId
                && loan.Status == LoanStatus.Active));

    public ValueTask<IReadOnlyList<CopyId>> OnActiveLoanAmongAsync(
        IReadOnlyCollection<CopyId> copyIds,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<CopyId>>(_loans.Values
            .Where(loan => loan.Status == LoanStatus.Active && copyIds.Contains(loan.CopyId))
            .Select(loan => loan.CopyId)
            .ToList());

    public void Add(Loan loan) => _loans[loan.Id] = loan;
}

internal sealed class InMemoryHoldQueueRepository : IHoldQueueRepository
{
    private readonly Dictionary<EditionId, HoldQueue> _queues = [];

    public IReadOnlyCollection<HoldQueue> Added => _queues.Values;

    public InMemoryHoldQueueRepository With(params HoldQueue[] queues)
    {
        foreach (var queue in queues)
        {
            _queues[queue.Id] = queue;
        }

        return this;
    }

    public ValueTask<HoldQueue?> GetByEditionAsync(
        EditionId editionId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_queues.GetValueOrDefault(editionId));

    public ValueTask<int> CountLiveForBorrowerAsync(
        BorrowerId borrowerId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_queues.Values
            .SelectMany(queue => queue.Holds)
            .Count(hold => hold.BorrowerId == borrowerId));

    public void Add(HoldQueue queue) => _queues[queue.Id] = queue;
}

// Stands in for Holdings across the boundary: which copies exist, whether each may be lent, and
// of what edition. That a stub this small is enough is the measure of how little crosses.
internal sealed class StubShelf : ICopyLendability
{
    private readonly Dictionary<Guid, LendabilityAnswer> _copies = [];
    private readonly Dictionary<Guid, List<Guid>> _lendableByEdition = [];

    public StubShelf Lendable(Guid copyId, Guid editionId)
    {
        _copies[copyId] = new LendabilityAnswer(Lendability.Lendable, editionId);
        _lendableByEdition.TryAdd(editionId, []);
        _lendableByEdition[editionId].Add(copyId);
        return this;
    }

    public StubShelf NotLendable(Guid copyId, Guid editionId)
    {
        _copies[copyId] = new LendabilityAnswer(Lendability.NotLendable, editionId);
        return this;
    }

    public ValueTask<LendabilityAnswer> OfAsync(
        Guid copyId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_copies.GetValueOrDefault(
            copyId,
            new LendabilityAnswer(Lendability.NoSuchCopy, null)));

    public ValueTask<IReadOnlyList<Guid>> LendableCopiesOfAsync(
        Guid editionId,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<Guid>>(
            _lendableByEdition.GetValueOrDefault(editionId, []));
}

// Stands in for Members: enrolled people answer Entitled, named lapsed ones answer Lapsed, and
// everyone else is a mis-scan.
internal sealed class StubRegistry : IMemberEntitlement
{
    private readonly HashSet<Guid> _entitled = [];
    private readonly HashSet<Guid> _lapsed = [];

    public StubRegistry Entitled(params Guid[] memberIds)
    {
        foreach (var memberId in memberIds)
        {
            _entitled.Add(memberId);
        }

        return this;
    }

    public StubRegistry Lapsed(Guid memberId)
    {
        _lapsed.Add(memberId);
        return this;
    }

    public ValueTask<EntitlementAnswer> OfAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        if (_entitled.Contains(memberId))
        {
            return ValueTask.FromResult(
                new EntitlementAnswer(Entitlement.Entitled, MemberCategory.Adult));
        }

        return ValueTask.FromResult(_lapsed.Contains(memberId)
            ? new EntitlementAnswer(Entitlement.Lapsed, null)
            : new EntitlementAnswer(Entitlement.NoSuchMember, null));
    }
}

// Stands in for Charges, which does not exist yet — exactly the position the host is in. Nobody
// owes anything unless a test says so.
internal sealed class StubBalances : IMemberBalance
{
    private readonly Dictionary<Guid, decimal> _owed = [];

    public StubBalances Owing(Guid memberId, decimal amount)
    {
        _owed[memberId] = amount;
        return this;
    }

    public ValueTask<decimal> OwedByAsync(Guid memberId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_owed.GetValueOrDefault(memberId));
}

// The clock the handlers read today from, held still.
internal sealed class FrozenClock(DateTimeOffset now) : TimeProvider
{
    public static FrozenClock At(DateOnly day)
        => new(new DateTimeOffset(day, TimeOnly.MinValue, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => now;
}
