using LibraryManagement.Members.Application.Members.GetMemberById;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Members.Infrastructure.Queries;

namespace LibraryManagement.Members.Infrastructure.Tests.Queries;

/// <summary>
/// The member's identity half of the desk's file, asked of the real store.
/// </summary>
/// <remarks>
/// The projection is what is under test, and it is the reason this cannot be a unit test: the
/// name and the guardian are optional complex values, and whether the store can tell an absent one
/// from an absent column is a question only the store answers.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class GetMemberByIdQueryHandlerTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MemberName AName(string given = "Antoine", string family = "Doinel") =>
        MemberName.Create(given, family)
            .Match(name => name, _ => throw new InvalidOperationException());

    private static CardNumber ACardNumber() =>
        CardNumber.Create(Guid.NewGuid().ToString("N")[..12])
            .Match(number => number, _ => throw new InvalidOperationException());

    private static ContactDetails AContact() =>
        ContactDetails.Create("antoine.doinel@example.org", "0612345678", "12 rue des Martyrs")
            .Match(contact => contact, _ => throw new InvalidOperationException());

    private static Member AMember(
        MemberCategory category = MemberCategory.Adult,
        ContactDetails? contact = null,
        Guardian? guardian = null)
        => Member.Enroll(
                MemberId.Generate(),
                AName(),
                new DateOnly(1990, 5, 1),
                category,
                ACardNumber(),
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

    private async Task<MemberDetailsDto> ReadAsync(Guid memberId)
    {
        await using var reading = sqlServer.NewContext();

        var result = await new GetMemberByIdQueryHandler(reading)
            .Handle(new GetMemberByIdQuery(memberId), Token);

        return result.Match(
            details => details,
            errors => throw new InvalidOperationException(errors[0].ErrorMessage));
    }

    [Fact]
    public async Task AMember_ComesBackAsTheDeskReadsThem()
    {
        var member = await StoredAsync(AMember(contact: AContact()));

        var file = await ReadAsync(member.Id.Value);

        file.MemberId.ShouldBe(member.Id.Value);
        file.GivenName.ShouldBe("Antoine");
        file.FamilyName.ShouldBe("Doinel");
        file.CardNumber.ShouldBe(member.CardNumber!.Value);
        file.MembershipStart.ShouldBe(Today);
        file.MembershipEnd.ShouldBe(member.MembershipEnd);
        file.Email.ShouldBe("antoine.doinel@example.org");
        file.Phone.ShouldBe("0612345678");
        file.PostalAddress.ShouldBe("12 rue des Martyrs");
        file.ErasedOn.ShouldBeNull();
    }

    [Fact]
    public async Task ACategory_TravelsAsItsNameAndNotItsNumber()
    {
        var member = await StoredAsync(AMember(
            MemberCategory.Child,
            guardian: Guardian.Of(AName("Colette", "Doinel"), AContact())));

        (await ReadAsync(member.Id.Value)).Category.ShouldBe("Child");
    }

    [Fact]
    public async Task AGuardian_ComesBackWhenThereIsOne()
    {
        var member = await StoredAsync(AMember(
            MemberCategory.Child,
            guardian: Guardian.Of(AName("Colette", "Doinel"), AContact())));

        var guardian = (await ReadAsync(member.Id.Value)).Guardian.ShouldNotBeNull();

        guardian.GivenName.ShouldBe("Colette");
        guardian.FamilyName.ShouldBe("Doinel");
        guardian.Email.ShouldBe("antoine.doinel@example.org");
    }

    [Fact]
    public async Task NoGuardian_ComesBackAsNothingRatherThanAnEmptyOne()
    {
        // The presence column earning its keep. Every guardian column is nullable, so without it
        // the store could not tell "reached directly" from "a guardian we know nothing about", and
        // this would come back as an object of nulls.
        var member = await StoredAsync(AMember());

        (await ReadAsync(member.Id.Value)).Guardian.ShouldBeNull();
    }

    [Fact]
    public async Task AnErasedMember_Answers_AndSaysWhenTheyWereErased()
    {
        // The decision this file exists to hold. Every command refuses an erased member; the read
        // does not, because the loans and charges keyed to that identifier stay countable and a
        // librarian holding one of them has to be told why the name is missing.
        var member = AMember(contact: AContact());
        member.Erase(Today).HasErrors().ShouldBeFalse();
        await StoredAsync(member);

        var file = await ReadAsync(member.Id.Value);

        file.ErasedOn.ShouldBe(Today);
        file.GivenName.ShouldBeNull();
        file.FamilyName.ShouldBeNull();
        file.CardNumber.ShouldBeNull();
        file.Email.ShouldBeNull();
    }

    [Fact]
    public async Task AMemberNobodyEnrolled_IsARefusalAndNotAnEmptyAnswer()
    {
        await using var reading = sqlServer.NewContext();

        var result = await new GetMemberByIdQueryHandler(reading)
            .Handle(new GetMemberByIdQuery(Guid.CreateVersion7()), Token);

        result.Match(_ => (string?)null, errors => errors[0].ErrorCode.Value)
            .ShouldBe(MembersErrorCodes.MemberNotFound.Value);
    }
}
