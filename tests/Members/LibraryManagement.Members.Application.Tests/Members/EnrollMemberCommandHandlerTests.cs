using LibraryManagement.Members.Application.Members;
using LibraryManagement.Members.Application.Members.EnrollMember;
using LibraryManagement.Members.Application.Tests.TestDoubles;
using LibraryManagement.Members.Domain.Members;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Members.Application.Tests.Members;

public sealed class EnrollMemberCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryMemberRepository _members = new();

    private ValueTask<Result> Handle(EnrollMemberCommand command)
        => new EnrollMemberCommandHandler(_members, FrozenClock.At(Today))
            .Handle(command, Token);

    private static EnrollMemberCommand AnEnrollment(
        MemberCategory category = MemberCategory.Adult,
        string cardNumber = "20260000512",
        GuardianDetails? guardian = null)
        => new(
            Guid.CreateVersion7(),
            "Antoine",
            "Doinel",
            new DateOnly(1990, 5, 1),
            category,
            cardNumber,
            Email: "antoine.doinel@example.org",
            Guardian: guardian);

    private static List<ErrorCode> CodesOf(Result outcome)
        => outcome.Match(() => [], errors => errors.Select(error => error.ErrorCode).ToList());

    [Fact]
    public async Task Handle_EnrollsTheMember_AndStartsTheMembershipToday()
    {
        var outcome = await Handle(AnEnrollment());

        outcome.HasErrors().ShouldBeFalse();

        var member = _members.Added.Single();
        member.Name!.ToString().ShouldBe("Antoine Doinel");
        member.CardNumber!.Value.ShouldBe("20260000512");
        member.MembershipStart.ShouldBe(Today);
        member.MembershipEnd.ShouldBe(Today.AddMonths(Member.MembershipDurationInMonths));
    }

    [Fact]
    public async Task Handle_KeepsTheIdentifierTheCallerChose()
    {
        var command = AnEnrollment();

        await Handle(command);

        _members.Added.Single().Id.Value.ShouldBe(command.MemberId);
    }

    [Fact]
    public async Task Handle_CarriesTheGuardianThrough()
    {
        var outcome = await Handle(AnEnrollment(
            MemberCategory.Child,
            guardian: new GuardianDetails("Gilberte", "Doinel", Phone: "0612345678")));

        outcome.HasErrors().ShouldBeFalse();

        var guardian = _members.Added.Single().Guardian.ShouldNotBeNull();
        guardian.Name.ToString().ShouldBe("Gilberte Doinel");
        guardian.Contact.Phone.ShouldBe("0612345678");
    }

    [Fact]
    public async Task Handle_ACardNumberAlreadyAssigned_FailsNamingTheNumber()
    {
        _members.With(AnEnrolledMember("20260000512"));

        var outcome = await Handle(AnEnrollment(cardNumber: "20260000512"));

        CodesOf(outcome).ShouldContain(MembersErrorCodes.CardNumberAlreadyInUse);
        outcome.Match(() => "", errors => errors[0].ErrorMessage)
            .ShouldContain("20260000512");
    }

    [Fact]
    public async Task Handle_ReportsTheNameTheCardAndTheGuardianAtOnce()
    {
        // A librarian filling an enrollment form is told everything wrong with it in one refusal,
        // not one mistake per attempt.
        var outcome = await Handle(new EnrollMemberCommand(
            Guid.CreateVersion7(),
            GivenName: " ",
            FamilyName: "Doinel",
            new DateOnly(1990, 5, 1),
            MemberCategory.Child,
            CardNumber: "1",
            Guardian: new GuardianDetails(" ", "Doinel")));

        var codes = CodesOf(outcome);
        codes.ShouldContain(MembersErrorCodes.InvalidMemberName);
        codes.ShouldContain(MembersErrorCodes.InvalidCardNumber);
    }

    [Fact]
    public async Task Handle_AChildWithoutAGuardian_IsRefusedByTheAggregate()
    {
        var outcome = await Handle(AnEnrollment(MemberCategory.Child));

        CodesOf(outcome).ShouldContain(MembersErrorCodes.GuardianRequired);
        _members.Added.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ABirthDateOnOrAfterToday_IsRefusedAgainstTheClock()
    {
        var outcome = await Handle(AnEnrollment() with { DateOfBirth = Today });

        CodesOf(outcome).ShouldContain(MembersErrorCodes.DateOfBirthNotInThePast);
    }

    private static Member AnEnrolledMember(string cardNumber)
        => Member.Enroll(
                MemberId.Generate(),
                MemberName.Create("Georges", "Perec")
                    .Match(name => name, _ => throw new InvalidOperationException()),
                new DateOnly(1936, 3, 7),
                MemberCategory.Adult,
                CardNumber.Create(cardNumber)
                    .Match(number => number, _ => throw new InvalidOperationException()),
                ContactDetails.None,
                guardian: null,
                new DateOnly(2026, 1, 5))
            .Match(member => member, _ => throw new InvalidOperationException());
}
