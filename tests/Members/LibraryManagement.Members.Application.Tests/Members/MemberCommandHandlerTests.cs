using LibraryManagement.Members.Application.Members;
using LibraryManagement.Members.Application.Members.ChangeMemberCategory;
using LibraryManagement.Members.Application.Members.ChangeMemberGuardian;
using LibraryManagement.Members.Application.Members.RenameMember;
using LibraryManagement.Members.Application.Members.RenewMembership;
using LibraryManagement.Members.Application.Members.ReplaceMemberCard;
using LibraryManagement.Members.Application.Members.UpdateMemberContactDetails;
using LibraryManagement.Members.Application.Tests.TestDoubles;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Tests.Members;

/// <summary>
/// The handlers that load a member and ask them to change. Each is three lines, and each has the
/// same two things worth pinning: it finds the member the caller named, and it says so plainly
/// when there is nobody.
/// </summary>
public sealed class MemberCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryMemberRepository _members = new();

    private static MemberName AName(string given = "Antoine", string family = "Doinel") =>
        MemberName.Create(given, family)
            .Match(name => name, _ => throw new InvalidOperationException());

    private static CardNumber ACardNumber(string value = "20260000512") =>
        CardNumber.Create(value).Match(number => number, _ => throw new InvalidOperationException());

    private Member AnEnrolledMember(
        MemberCategory category = MemberCategory.Adult,
        Guardian? guardian = null,
        string cardNumber = "20260000512",
        DateOnly? enrolledOn = null)
    {
        var member = Member.Enroll(
                MemberId.Generate(),
                AName(),
                new DateOnly(1990, 5, 1),
                category,
                ACardNumber(cardNumber),
                ContactDetails.None,
                guardian,
                enrolledOn ?? Today)
            .Match(enrolled => enrolled, _ => throw new InvalidOperationException());

        _members.With(member);
        return member;
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    // --- Each handler does what its command says ---------------------------------------------------

    [Fact]
    public async Task Renew_BeforeExpiry_ExtendsFromTheOldEnd()
    {
        var member = AnEnrolledMember();
        var oldEnd = member.MembershipEnd;

        var outcome = await new RenewMembershipCommandHandler(_members, FrozenClock.At(Today))
            .Handle(new RenewMembershipCommand(member.Id.Value), Token);

        outcome.HasErrors().ShouldBeFalse();
        member.MembershipEnd.ShouldBe(oldEnd.AddMonths(Member.MembershipDurationInMonths));
    }

    [Fact]
    public async Task Renew_LongAfterExpiry_StartsAgainFromTheClock()
    {
        // The member enrolled years ago and comes back: a renewal, never a re-enrollment — the
        // clock decides which arithmetic, the aggregate owns both.
        var member = AnEnrolledMember(enrolledOn: new DateOnly(2020, 3, 14));

        var outcome = await new RenewMembershipCommandHandler(_members, FrozenClock.At(Today))
            .Handle(new RenewMembershipCommand(member.Id.Value), Token);

        outcome.HasErrors().ShouldBeFalse();
        member.MembershipStart.ShouldBe(Today);
        member.MembershipEnd.ShouldBe(Today.AddMonths(Member.MembershipDurationInMonths));
    }

    [Fact]
    public async Task ChangeCategory_MovesTheMember()
    {
        var member = AnEnrolledMember();

        var outcome = await new ChangeMemberCategoryCommandHandler(_members)
            .Handle(new ChangeMemberCategoryCommand(member.Id.Value, MemberCategory.Student), Token);

        outcome.HasErrors().ShouldBeFalse();
        member.Category.ShouldBe(MemberCategory.Student);
    }

    [Fact]
    public async Task ReplaceCard_ReplacesTheNumber()
    {
        var member = AnEnrolledMember();

        var outcome = await new ReplaceMemberCardCommandHandler(_members)
            .Handle(new ReplaceMemberCardCommand(member.Id.Value, "20260000999"), Token);

        outcome.HasErrors().ShouldBeFalse();
        member.CardNumber.Value.ShouldBe("20260000999");
    }

    [Fact]
    public async Task ReplaceCard_WithANumberAlreadyOnAnotherCard_Fails()
    {
        var member = AnEnrolledMember();
        AnEnrolledMember(cardNumber: "20260000999");

        var outcome = await new ReplaceMemberCardCommandHandler(_members)
            .Handle(new ReplaceMemberCardCommand(member.Id.Value, "20260000999"), Token);

        CodesOf(outcome).ShouldContain(MembersErrorCodes.CardNumberAlreadyInUse);
        member.CardNumber.Value.ShouldBe("20260000512");
    }

    [Fact]
    public async Task ReplaceCard_WithTheNumberItAlreadyCarries_IsNotACollisionWithItself()
    {
        var member = AnEnrolledMember();

        var outcome = await new ReplaceMemberCardCommandHandler(_members)
            .Handle(new ReplaceMemberCardCommand(member.Id.Value, "20260000512"), Token);

        outcome.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateContactDetails_RecordsTheChannels()
    {
        var member = AnEnrolledMember();

        var outcome = await new UpdateMemberContactDetailsCommandHandler(_members)
            .Handle(
                new UpdateMemberContactDetailsCommand(
                    member.Id.Value, Email: "antoine.doinel@example.org"),
                Token);

        outcome.HasErrors().ShouldBeFalse();
        member.ContactDetails.Email.ShouldBe("antoine.doinel@example.org");
    }

    [Fact]
    public async Task ChangeGuardian_RecordsTheGuardian()
    {
        var member = AnEnrolledMember();

        var outcome = await new ChangeMemberGuardianCommandHandler(_members)
            .Handle(
                new ChangeMemberGuardianCommand(
                    member.Id.Value, new GuardianDetails("Gilberte", "Doinel")),
                Token);

        outcome.HasErrors().ShouldBeFalse();
        member.Guardian.ShouldNotBeNull().Name.ToString().ShouldBe("Gilberte Doinel");
    }

    [Fact]
    public async Task ChangeGuardian_RemovingAChilds_IsRefused()
    {
        var member = AnEnrolledMember(
            MemberCategory.Child,
            Guardian.Of(AName("Gilberte", "Doinel"), ContactDetails.None));

        var outcome = await new ChangeMemberGuardianCommandHandler(_members)
            .Handle(new ChangeMemberGuardianCommand(member.Id.Value, null), Token);

        CodesOf(outcome).ShouldContain(MembersErrorCodes.GuardianRequired);
        member.Guardian.ShouldNotBeNull();
    }

    [Fact]
    public async Task Rename_RecordsTheNewName()
    {
        var member = AnEnrolledMember();

        var outcome = await new RenameMemberCommandHandler(_members)
            .Handle(new RenameMemberCommand(member.Id.Value, "Antoine", "Doinel-Montag"), Token);

        outcome.HasErrors().ShouldBeFalse();
        member.Name.FamilyName.ShouldBe("Doinel-Montag");
    }

    // --- And each says so plainly when there is nobody ---------------------------------------------

    [Fact]
    public async Task EveryHandler_SaysSoPlainly_WhenNobodyIsEnrolledUnderTheIdentifier()
    {
        var nobody = Guid.CreateVersion7();

        List<Result> outcomes =
        [
            await new RenewMembershipCommandHandler(_members, FrozenClock.At(Today))
                .Handle(new RenewMembershipCommand(nobody), Token),
            await new ChangeMemberCategoryCommandHandler(_members)
                .Handle(new ChangeMemberCategoryCommand(nobody, MemberCategory.Adult), Token),
            await new ReplaceMemberCardCommandHandler(_members)
                .Handle(new ReplaceMemberCardCommand(nobody, "20260000999"), Token),
            await new UpdateMemberContactDetailsCommandHandler(_members)
                .Handle(new UpdateMemberContactDetailsCommand(nobody), Token),
            await new ChangeMemberGuardianCommandHandler(_members)
                .Handle(new ChangeMemberGuardianCommand(nobody, null), Token),
            await new RenameMemberCommandHandler(_members)
                .Handle(new RenameMemberCommand(nobody, "Antoine", "Doinel"), Token),
        ];

        foreach (var outcome in outcomes)
        {
            CodesOf(outcome).ShouldContain(MembersErrorCodes.MemberNotFound);
        }
    }
}
