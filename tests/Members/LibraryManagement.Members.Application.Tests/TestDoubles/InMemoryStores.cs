using LibraryManagement.Members.Domain.Members;

namespace LibraryManagement.Members.Application.Tests.TestDoubles;

// Nothing is saved here, which is exactly right: a handler never writes. It hands work to the
// store, and the module's unit of work writes once the command has succeeded.
internal sealed class InMemoryMemberRepository : IMemberRepository
{
    private readonly Dictionary<MemberId, Member> _members = [];

    public IReadOnlyCollection<Member> Added => _members.Values;

    public InMemoryMemberRepository With(params Member[] members)
    {
        foreach (var member in members)
        {
            _members[member.Id] = member;
        }

        return this;
    }

    public ValueTask<Member?> GetByIdAsync(MemberId id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_members.GetValueOrDefault(id));

    public ValueTask<bool> CardNumberIsTakenAsync(
        CardNumber cardNumber,
        MemberId? except = null,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(
            _members.Values.Any(member => member.CardNumber == cardNumber && member.Id != except));

    public void Add(Member member) => _members[member.Id] = member;
}

// The clock the handlers read today from, held still. The domain has no clock at all — it is
// handed a day — so freezing the handler's is all it takes to make every date in these tests a
// plain assertion.
internal sealed class FrozenClock(DateTimeOffset now) : TimeProvider
{
    public static FrozenClock At(DateOnly day)
        => new(new DateTimeOffset(day, TimeOnly.MinValue, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => now;
}
