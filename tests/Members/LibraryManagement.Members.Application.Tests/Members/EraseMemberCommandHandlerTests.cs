using LibraryManagement.Members.Application.Members.EraseMember;
using LibraryManagement.Members.Application.Tests.TestDoubles;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Tests.Members;

/// <summary>
/// The member asked to be forgotten: the record empties, the identifier stands, and the day is
/// the clock's.
/// </summary>
public sealed class EraseMemberCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryMemberRepository _members = new();

    private ValueTask<Result> Handle(Guid memberId)
        => new EraseMemberCommandHandler(_members, FrozenClock.At(Today))
            .Handle(new EraseMemberCommand(memberId), Token);

    private Member AnEnrolledMember()
    {
        var member = Member.Enroll(
                MemberId.Generate(),
                MemberName.Create("Antoine", "Doinel")
                    .Match(name => name, _ => throw new InvalidOperationException()),
                new DateOnly(1990, 5, 1),
                MemberCategory.Adult,
                CardNumber.Create("20260000512")
                    .Match(number => number, _ => throw new InvalidOperationException()),
                ContactDetails.None,
                guardian: null,
                Today)
            .Match(enrolled => enrolled, _ => throw new InvalidOperationException());

        _members.With(member);
        return member;
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_EmptiesTheRecord_OnTheClocksDay()
    {
        var member = AnEnrolledMember();

        var outcome = await Handle(member.Id.Value);

        outcome.HasErrors().ShouldBeFalse();
        member.ErasedOn.ShouldBe(Today);
        member.Name.ShouldBeNull();
        member.CardNumber.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_AMemberNobodyEnrolled_IsRefused()
    {
        CodesOf(await Handle(Guid.CreateVersion7()))
            .ShouldContain(MembersErrorCodes.MemberNotFound);
    }

    [Fact]
    public async Task Handle_AnErasureAlreadyDone_AnswersSuccess()
    {
        var member = AnEnrolledMember();
        member.Erase(Today.AddDays(-30));
        member.ClearDomainEvents();

        var outcome = await Handle(member.Id.Value);

        outcome.HasErrors().ShouldBeFalse();
        member.ErasedOn.ShouldBe(Today.AddDays(-30));
        member.DomainEvents.ShouldBeEmpty();
    }
}
