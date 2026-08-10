using LibraryManagement.Members.Application.Members.MergeMembers;
using LibraryManagement.Members.Application.Tests.TestDoubles;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Tests.Members;

/// <summary>
/// The store questions a merge asks, and nothing about which records may be joined — that is the
/// domain service's, and this class injects the real one so a refusal is propagated rather than
/// simulated.
/// </summary>
public sealed class MergeMembersCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryMemberRepository _members = new();

    private readonly MemberMergeDomainService _merge = new();

    private int _cards;

    private ValueTask<Result> Handle(Guid absorbed, Guid surviving)
        => new MergeMembersCommandHandler(_members, _merge)
            .Handle(new MergeMembersCommand(absorbed, surviving), Token);

    private Member AnEnrolledMember()
    {
        var member = Member.Enroll(
                MemberId.Generate(),
                MemberName.Create("Antoine", "Doinel")
                    .Match(name => name, _ => throw new InvalidOperationException()),
                new DateOnly(1990, 5, 1),
                MemberCategory.Adult,
                CardNumber.Create($"2026000{_cards++:D4}")
                    .Match(number => number, _ => throw new InvalidOperationException()),
                ContactDetails.None,
                guardian: null,
                Today)
            .Match(enrolled => enrolled, _ => throw new InvalidOperationException());

        _members.With(member);
        member.ClearDomainEvents();

        return member;
    }

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_JoinsTheTwoRecords()
    {
        var absorbed = AnEnrolledMember();
        var surviving = AnEnrolledMember();

        var outcome = await Handle(absorbed.Id.Value, surviving.Id.Value);

        outcome.HasErrors().ShouldBeFalse();
        absorbed.MergedInto.ShouldBe(surviving.Id);
        absorbed.DomainEvents.OfType<MembersMerged>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Handle_LeavesTheSurvivorUntouched()
    {
        var absorbed = AnEnrolledMember();
        var surviving = AnEnrolledMember();

        await Handle(absorbed.Id.Value, surviving.Id.Value);

        surviving.MergedInto.ShouldBeNull();
        surviving.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ReportsBothMissingRecordsAtOnce()
    {
        // Somebody who mistyped one identifier may well have mistyped the other, and finding out
        // one at a time is two trips.
        var outcome = await Handle(Guid.CreateVersion7(), Guid.CreateVersion7());

        var errors = outcome.Match(() => [], errors => errors.ToList());
        errors.Count.ShouldBe(2);
        errors.ShouldAllBe(error => error.ErrorCode == MembersErrorCodes.MemberNotFound);
    }

    [Fact]
    public async Task Handle_ARecordThatDoesNotResolve_IsRefusedBeforeAnythingChanges()
    {
        var surviving = AnEnrolledMember();

        CodesOf(await Handle(Guid.CreateVersion7(), surviving.Id.Value))
            .ShouldContain(MembersErrorCodes.MemberNotFound);

        surviving.MergedInto.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_PropagatesTheServicesRefusal()
    {
        // What this checks is that the handler passes a refusal it did not make. A stub would turn
        // that into an assertion about the stub.
        var member = AnEnrolledMember();

        CodesOf(await Handle(member.Id.Value, member.Id.Value))
            .ShouldContain(MembersErrorCodes.MemberCannotAbsorbItself);
    }

    [Fact]
    public async Task Handle_DemandsACommand()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await new MergeMembersCommandHandler(_members, _merge).Handle(null!, Token));
    }
}
