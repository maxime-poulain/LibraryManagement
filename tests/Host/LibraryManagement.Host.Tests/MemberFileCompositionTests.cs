using LibraryManagement.Charges.Application.Accounts.GetMemberBalance;
using LibraryManagement.Circulation.Application.Borrowers.GetBorrowerFile;
using LibraryManagement.Host.Http;
using LibraryManagement.Members.Application.Members.GetMemberById;
using LibraryManagement.Shared.Application.CQS;
using LibraryManagement.Shared.Domain.Errors;
using LibraryManagement.Shared.Domain.Results;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LibraryManagement.Host.Tests;

/// <summary>
/// The composition itself: three answers in, one page out — and what the page does when one of the
/// three does not arrive.
/// </summary>
/// <remarks>
/// No database and no host. The modules' answers are handed in directly, because what is under
/// test is not what they say but what the composer does with it, and
/// <c>docs/adr/0016-a-composed-page-degrades-in-parts.md</c> is a decision that ought to be held by
/// the gate that blocks a merge rather than by the suite that needs Docker.
/// </remarks>
public sealed class MemberFileCompositionTests
{
    private static readonly Guid TheMember = Guid.CreateVersion7();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static MemberDetailsDto Details => new(
        TheMember,
        "Antoine",
        "Doinel",
        "Adult",
        "C-0042",
        new DateOnly(2026, 1, 1),
        new DateOnly(2027, 1, 1),
        "antoine.doinel@example.org",
        Phone: null,
        PostalAddress: null,
        Guardian: null,
        ErasedOn: null);

    private static BorrowerFileDto Circulation => new(TheMember, [], [], DebtBlocksBorrowing: false);

    private static MemberBalanceDto Charges => new(TheMember, 4.20m);

    private static Result<T> Refused<T>(string code)
        => Result<T>.Failure(new ErrorCode(code), $"{code} happened.");

    /// <summary>The three modules, standing in — each answering whatever the test hands it.</summary>
    private sealed class Answers(
        Result<MemberDetailsDto> member,
        Result<BorrowerFileDto> circulation,
        Result<MemberBalanceDto> charges) : IQueryDispatcher
    {
        public ValueTask<Result<TValue>> DispatchAsync<TValue>(
            IQuery<TValue> query,
            CancellationToken cancellationToken = default)
        {
            object answer = query switch
            {
                GetMemberByIdQuery => member,
                GetBorrowerFileQuery => circulation,
                GetMemberBalanceQuery => charges,
                _ => throw new InvalidOperationException($"Unexpected query: {query.GetType().Name}."),
            };

            return ValueTask.FromResult((Result<TValue>)answer);
        }
    }

    private static Task<IResult> ComposeAsync(
        Result<MemberDetailsDto>? member = null,
        Result<BorrowerFileDto>? circulation = null,
        Result<MemberBalanceDto>? charges = null)
        => MemberFileEndpoints.ComposeAsync(
            TheMember,
            new Answers(
                member ?? Result<MemberDetailsDto>.Success(Details),
                circulation ?? Result<BorrowerFileDto>.Success(Circulation),
                charges ?? Result<MemberBalanceDto>.Success(Charges)),
            Token);

    private static async Task<MemberFileResponse> FileAsync(
        Result<BorrowerFileDto>? circulation = null,
        Result<MemberBalanceDto>? charges = null)
        => (await ComposeAsync(circulation: circulation, charges: charges))
            .ShouldBeAssignableTo<Ok<MemberFileResponse>>()
            .Value
            .ShouldNotBeNull();

    [Fact]
    public async Task ThreeAnswers_BecomeOnePage()
    {
        var file = await FileAsync();

        file.Member.MemberId.ShouldBe(TheMember);
        file.Circulation.ShouldNotBeNull();
        file.Charges.ShouldNotBeNull().Balance.ShouldBe(4.20m);
        file.Unavailable.ShouldBeEmpty();
    }

    [Fact]
    public async Task AMemberNobodyEnrolled_EmptiesThePage()
    {
        // The one refusal that is the caller's, because a file is a file *of* somebody. It travels
        // as the mapper's 404 rather than as a page with three blanks in it.
        var response = await ComposeAsync(
            member: Refused<MemberDetailsDto>("Members.MemberNotFound"));

        response.ShouldBeAssignableTo<IStatusCodeHttpResult>()
            .StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task AModuleThatCannotAnswer_LeavesItsPanelBlankAndSaysSo()
    {
        var file = await FileAsync(charges: Refused<MemberBalanceDto>("Charges.Unreachable"));

        // The decision ADR-0016 records: the desk sees the rest of the file, and sees that the
        // money is missing rather than reading a blank as a zero.
        file.Charges.ShouldBeNull();
        file.Member.ShouldNotBeNull();
        file.Circulation.ShouldNotBeNull();

        var missing = file.Unavailable.ShouldHaveSingleItem();
        missing.Part.ShouldBe("charges");
        missing.Code.ShouldBe("Charges.Unreachable");
    }

    [Fact]
    public async Task BothOtherModulesFailing_StillLeavesTheMemberOnThePage()
    {
        var file = await FileAsync(
            circulation: Refused<BorrowerFileDto>("Circulation.Unreachable"),
            charges: Refused<MemberBalanceDto>("Charges.Unreachable"));

        file.Member.GivenName.ShouldBe("Antoine");
        file.Circulation.ShouldBeNull();
        file.Charges.ShouldBeNull();
        file.Unavailable.Select(part => part.Part).ShouldBe(["circulation", "charges"]);
    }

    [Fact]
    public async Task TheComposer_DisplaysTheStandingAndNeverDerivesIt()
    {
        // The rule this file exists to keep out. Circulation says the debt does not block; the
        // balance says 4.20 is owed. A composer that judged for itself would have to pick, and
        // whichever it picked would be a rule no module's invariants cover.
        var file = await FileAsync(
            circulation: Result<BorrowerFileDto>.Success(
                new BorrowerFileDto(TheMember, [], [], DebtBlocksBorrowing: false)),
            charges: Result<MemberBalanceDto>.Success(new MemberBalanceDto(TheMember, 999m)));

        file.Circulation.ShouldNotBeNull().DebtBlocksBorrowing.ShouldBeFalse();
        file.Charges.ShouldNotBeNull().Balance.ShouldBe(999m);
    }
}
