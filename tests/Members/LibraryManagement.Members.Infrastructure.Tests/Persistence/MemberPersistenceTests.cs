using LibraryManagement.Members.Domain.Members;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagement.Members.Infrastructure.Tests.Persistence;

[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class MemberPersistenceTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MemberName AName(string given = "Antoine", string family = "Doinel") =>
        MemberName.Create(given, family)
            .Match(name => name, _ => throw new InvalidOperationException());

    private static CardNumber ACardNumber(string value) =>
        CardNumber.Create(value).Match(number => number, _ => throw new InvalidOperationException(value));

    private static ContactDetails AContact() =>
        ContactDetails.Create("antoine.doinel@example.org", "0612345678", "12 rue des Martyrs")
            .Match(contact => contact, _ => throw new InvalidOperationException());

    // Version 4 rather than 7: these are card numbers, not identifiers, and three generated in
    // one millisecond must not share a prefix — which is exactly what a time-ordered UUID
    // guarantees they would.
    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    private static Member AMember(
        string cardNumber,
        MemberCategory category = MemberCategory.Adult,
        ContactDetails? contact = null,
        Guardian? guardian = null)
        => Member.Enroll(
                MemberId.Generate(),
                AName(),
                new DateOnly(1990, 5, 1),
                category,
                ACardNumber(cardNumber),
                contact ?? ContactDetails.None,
                guardian,
                Today)
            .Match(member => member, _ => throw new InvalidOperationException());

    private async Task<Member> StoredAsync(Member member)
    {
        await using var writing = sqlServer.NewContext();
        writing.Add(member);
        await writing.SaveChangesAsync(Token);
        return member;
    }

    [Fact]
    public async Task AMember_ComesBackWithEverythingTheyWereStoredWith()
    {
        var guardian = Guardian.Of(
            AName("Gilberte", "Doinel"),
            ContactDetails.Create("gilberte.doinel@example.org")
                .Match(contact => contact, _ => throw new InvalidOperationException()));

        var member = await StoredAsync(
            AMember(Unique(), MemberCategory.Child, AContact(), guardian));

        // A second context, so this proves the values reached the database rather than the change
        // tracker.
        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Member>().SingleAsync(stored => stored.Id == member.Id, Token);

        found.Name.ShouldBe(member.Name);
        found.DateOfBirth.ShouldBe(new DateOnly(1990, 5, 1));
        found.Category.ShouldBe(MemberCategory.Child);
        found.CardNumber.ShouldBe(member.CardNumber);
        found.MembershipStart.ShouldBe(Today);
        found.MembershipEnd.ShouldBe(Today.AddMonths(Member.MembershipDurationInMonths));
        found.ContactDetails.ShouldBe(AContact());
        found.Guardian.ShouldBe(guardian);
    }

    [Fact]
    public async Task AMemberReachedDirectly_ComesBackWithNoGuardian()
    {
        // The optional half of the mapping: all the guardian columns null, and the value absent
        // rather than empty on the way back.
        var member = await StoredAsync(AMember(Unique()));

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Member>().SingleAsync(stored => stored.Id == member.Id, Token);

        found.Guardian.ShouldBeNull();
        found.ContactDetails.ShouldBe(ContactDetails.None);
    }

    [Fact]
    public async Task TwoMembersSharingACardNumber_AreRefusedByTheStore()
    {
        // The handler asks first so the refusal can name the number. This index is what makes the
        // answer still true by the time it is written, and it is the rule's actual holder.
        var cardNumber = Unique();
        await StoredAsync(AMember(cardNumber));

        await using var writing = sqlServer.NewContext();
        writing.Add(AMember(cardNumber));

        await Should.ThrowAsync<DbUpdateException>(() => writing.SaveChangesAsync(Token));
    }

    [Fact]
    public async Task ARenewal_SurvivesTheRoundTrip()
    {
        var member = await StoredAsync(AMember(Unique()));

        await using (var updating = sqlServer.NewContext())
        {
            var loaded = await updating.Set<Member>()
                .SingleAsync(stored => stored.Id == member.Id, Token);
            loaded.Renew(Today.AddYears(2));
            await updating.SaveChangesAsync(Token);
        }

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Member>().SingleAsync(stored => stored.Id == member.Id, Token);

        found.MembershipStart.ShouldBe(Today.AddYears(2));
        found.MembershipEnd.ShouldBe(
            Today.AddYears(2).AddMonths(Member.MembershipDurationInMonths));
    }

    [Fact]
    public async Task AnErasedMember_ComesBackEmpty_AndStillARow()
    {
        // The presence bit on the name, the nullable card and birth date: an erased record must
        // survive a round trip as exactly what it is — an identifier that resolves to nobody.
        var member = AMember(cardNumber: Unique());
        member.Erase(Today);

        var stored = await StoredAsync(member);

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Member>().SingleAsync(held => held.Id == stored.Id, Token);

        found.ErasedOn.ShouldBe(Today);
        found.Name.ShouldBeNull();
        found.DateOfBirth.ShouldBeNull();
        found.CardNumber.ShouldBeNull();
        found.Guardian.ShouldBeNull();
        found.MembershipEnd.ShouldBe(member.MembershipEnd);
    }

    [Fact]
    public async Task TwoErasedMembers_Coexist_BecauseTheCardIndexIsFiltered()
    {
        // Erasure retires a card to null. An unfiltered unique index would allow exactly one
        // erased member in the whole registry, and the second erasure would fail at the save —
        // an accident discovered only in production, which is what this pins.
        var first = AMember(cardNumber: Unique());
        var second = AMember(cardNumber: Unique());
        first.Erase(Today);
        second.Erase(Today);

        await StoredAsync(first);
        await StoredAsync(second);

        await using var reading = sqlServer.NewContext();
        (await reading.Set<Member>().CountAsync(held => held.ErasedOn != null, Token))
            .ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task AMergedMember_ComesBackPointingAtItsSurvivor()
    {
        var absorbed = AMember(cardNumber: Unique());
        var surviving = AMember(cardNumber: Unique());
        new MemberMergeDomainService().Merge(absorbed, surviving);

        await StoredAsync(absorbed);
        await StoredAsync(surviving);

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Member>().SingleAsync(held => held.Id == absorbed.Id, Token);

        found.MergedInto.ShouldBe(surviving.Id);

        // A merge does not anonymize: the audit trail is the point, and the card stops working by
        // the entitlement answering "unknown" rather than by the column being emptied.
        found.Name.ShouldNotBeNull();
        found.CardNumber.ShouldNotBeNull();
    }

    [Fact]
    public async Task AMemberInItsOwnRight_HoldsNoPointer()
    {
        var stored = await StoredAsync(AMember(cardNumber: Unique()));

        await using var reading = sqlServer.NewContext();
        var found = await reading.Set<Member>().SingleAsync(held => held.Id == stored.Id, Token);

        found.MergedInto.ShouldBeNull();
    }
}
