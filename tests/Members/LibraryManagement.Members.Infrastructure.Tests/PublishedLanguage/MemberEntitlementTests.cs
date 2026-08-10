using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.PublishedLanguage;
using LibraryManagement.Members.PublishedLanguage;
using DomainCategory = LibraryManagement.Members.Domain.Members.MemberCategory;
using PublishedCategory = LibraryManagement.Members.PublishedLanguage.MemberCategory;

namespace LibraryManagement.Members.Infrastructure.Tests.PublishedLanguage;

/// <summary>
/// The surface Members publishes to the contexts downstream of it, exercised against a real
/// store.
/// </summary>
/// <remarks>
/// It earns integration tests rather than unit ones for the reason Catalog's port does:
/// everything that can go wrong here is the store's, starting with the identifier comparison a
/// value-converted key refuses to translate. And one thing more: the port re-states
/// <c>Member.MembershipIsCurrentOn</c> as a projection, and these tests are what keep the two in
/// step — the end day inclusive, most of all.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MemberEntitlementTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly EnrolledOn = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static Member AMember(
        DomainCategory category = DomainCategory.Adult,
        Guardian? guardian = null)
        => Member.Enroll(
                MemberId.Generate(),
                MemberName.Create("Antoine", "Doinel")
                    .Match(name => name, _ => throw new InvalidOperationException()),
                new DateOnly(1990, 5, 1),
                category,
                CardNumber.Create(Unique())
                    .Match(number => number, _ => throw new InvalidOperationException()),
                ContactDetails.None,
                guardian,
                EnrolledOn)
            .Match(member => member, _ => throw new InvalidOperationException());

    private async Task<Member> StoredAsync(Member member)
    {
        await using var writing = sqlServer.NewContext();
        writing.Add(member);
        await writing.SaveChangesAsync(Token);
        return member;
    }

    private async Task<EntitlementAnswer> AskedOn(DateOnly day, Guid memberId)
    {
        await using var reading = sqlServer.NewContext();

        return await new MemberEntitlement(reading, FrozenClock.At(day))
            .OfAsync(memberId, Token);
    }

    [Fact]
    public async Task AMemberWithinTheirPeriod_IsEntitled_AndTheCategoryRides()
    {
        var member = await StoredAsync(AMember());

        var answer = await AskedOn(EnrolledOn.AddMonths(6), member.Id.Value);

        answer.Entitlement.ShouldBe(Entitlement.Entitled);
        answer.Category.ShouldBe(PublishedCategory.Adult);
    }

    [Fact]
    public async Task TheEndDayItself_IsInsideThePeriod()
    {
        // The projection's reading of the rule the aggregate states as MembershipIsCurrentOn —
        // inclusive on both ends — proven against the store so the two cannot drift apart
        // silently.
        var member = await StoredAsync(AMember());

        var answer = await AskedOn(member.MembershipEnd, member.Id.Value);

        answer.Entitlement.ShouldBe(Entitlement.Entitled);
    }

    [Fact]
    public async Task AMemberPastTheirPeriod_IsLapsed_AndNoCategoryRides()
    {
        // Lapsed is not a refusal shaped like Entitled: the category is decided afresh at
        // renewal, not reported stale here.
        var member = await StoredAsync(AMember());

        var answer = await AskedOn(member.MembershipEnd.AddDays(1), member.Id.Value);

        answer.Entitlement.ShouldBe(Entitlement.Lapsed);
        answer.Category.ShouldBeNull();
    }

    [Fact]
    public async Task NobodyEnrolledUnderTheIdentifier_IsItsOwnAnswer()
    {
        // What Circulation acts on when a card matches nothing: a mis-scan or another library's
        // card, not a lapsed membership.
        var answer = await AskedOn(EnrolledOn, Guid.CreateVersion7());

        answer.Entitlement.ShouldBe(Entitlement.NoSuchMember);
        answer.Category.ShouldBeNull();
    }

    [Fact]
    public async Task TheCategoryCrossesTheBoundaryByValue()
    {
        var child = await StoredAsync(AMember(
            DomainCategory.Child,
            Guardian.Of(
                MemberName.Create("Gilberte", "Doinel")
                    .Match(name => name, _ => throw new InvalidOperationException()),
                ContactDetails.None)));

        var answer = await AskedOn(EnrolledOn, child.Id.Value);

        answer.Category.ShouldBe(PublishedCategory.Child);
    }

    [Fact]
    public async Task AnErasedMember_IsNoSuchMember()
    {
        // The identifier no longer resolves to a person, and the port answers exactly as it does
        // for an identifier that never did — which is what the erasure promised, and what keeps a
        // found card or a stale screen from acting in a ghost's name.
        var member = AMember();
        member.Erase(EnrolledOn);
        await StoredAsync(member);

        var answer = await AskedOn(EnrolledOn, member.Id.Value);

        answer.Entitlement.ShouldBe(Entitlement.NoSuchMember);
        answer.Category.ShouldBeNull();
    }

    [Fact]
    public async Task AMergedMember_AnswersAsAnUnknownOneToo()
    {
        // This one clause is what keeps a new loan or a new hold off an absorbed identifier: every
        // checkout and every hold asks this port first, so the card on the record somebody merged
        // away stops working the moment they say so. No consumer had to learn anything.
        var absorbed = AMember();
        var surviving = AMember();
        new MemberMergeDomainService().Merge(absorbed, surviving);
        await StoredAsync(absorbed);
        await StoredAsync(surviving);

        var answer = await AskedOn(EnrolledOn, absorbed.Id.Value);

        answer.Entitlement.ShouldBe(Entitlement.NoSuchMember);
        answer.Category.ShouldBeNull();

        // And the survivor is untouched, which is the half a one-sided assertion would miss.
        (await AskedOn(EnrolledOn, surviving.Id.Value)).Entitlement.ShouldBe(Entitlement.Entitled);
    }
}

/// <summary>
/// The clock the port reads today from, held still — entitlement is computed against the day of
/// the asking, and these tests choose the day.
/// </summary>
internal sealed class FrozenClock(DateTimeOffset now) : TimeProvider
{
    public static FrozenClock At(DateOnly day)
        => new(new DateTimeOffset(day, TimeOnly.MinValue, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => now;
}
