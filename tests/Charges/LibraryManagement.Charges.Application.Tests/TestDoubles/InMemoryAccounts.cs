namespace LibraryManagement.Charges.Application.Tests.TestDoubles;

/// <summary>
/// The accounts a handler acts on, held in memory.
/// </summary>
/// <remarks>
/// A dictionary rather than a mock, for the reason every double in this solution is one: a handler
/// test that asserts on calls proves the handler called something, and what these tests are about is
/// what the account ends up holding.
/// </remarks>
public sealed class InMemoryAccounts : IMemberAccountRepository
{
    private readonly Dictionary<MemberId, MemberAccount> _accounts = [];

    /// <summary>Whether the handler opened an account that did not exist.</summary>
    public bool Opened { get; private set; }

    public InMemoryAccounts With(MemberAccount account)
    {
        _accounts[account.Id] = account;
        return this;
    }

    /// <inheritdoc/>
    public Task<MemberAccount?> GetByMemberAsync(
        MemberId memberId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.GetValueOrDefault(memberId));

    /// <inheritdoc/>
    public Task<MemberAccount?> GetByOutstandingReplacementForAsync(
        CopyId copyId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.Values.FirstOrDefault(account =>
            account.Charges.OfType<ReplacementCharge>().Any(charge => charge.CopyId == copyId)));

    /// <inheritdoc/>
    public void Add(MemberAccount account)
    {
        Opened = true;
        _accounts[account.Id] = account;
    }
}

/// <summary>
/// A clock that does not move, because none of these rules is testable against one that does.
/// </summary>
public sealed class FrozenClock(DateOnly today) : TimeProvider
{
    public static FrozenClock At(DateOnly day) => new(day);

    public override DateTimeOffset GetUtcNow()
        => new(today, TimeOnly.MinValue, TimeSpan.Zero);
}
