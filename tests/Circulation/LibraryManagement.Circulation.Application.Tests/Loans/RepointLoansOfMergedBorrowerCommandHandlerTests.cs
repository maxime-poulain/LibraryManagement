using LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedBorrower;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

/// <summary>
/// What a merge in Members costs this context's loans: the ones not yet answered for answer to the
/// survivor, and the ones answered for keep saying who borrowed.
/// </summary>
public sealed class RepointLoansOfMergedBorrowerCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();

    private ValueTask<Result> Handle(BorrowerId absorbed, BorrowerId surviving)
        => new RepointLoansOfMergedBorrowerCommandHandler(_loans)
            .Handle(
                new RepointLoansOfMergedBorrowerCommand(absorbed.Value, surviving.Value),
                Token);

    private Loan ALoanOf(BorrowerId borrowerId)
    {
        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            EditionId.Generate(),
            borrowerId,
            Today,
            CirculationPolicy.Current);

        _loans.With(loan);
        loan.ClearDomainEvents();

        return loan;
    }

    [Fact]
    public async Task Handle_MovesEveryLoanTheAbsorbedRecordHasNotAnsweredFor()
    {
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var outstanding = new[] { ALoanOf(absorbed), ALoanOf(absorbed), ALoanOf(absorbed) };

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        outstanding.ShouldAllBe(loan => loan.BorrowerId == surviving);
        outstanding.ShouldAllBe(
            loan => loan.DomainEvents.OfType<LoanBorrowerRepointed>().Count() == 1);
    }

    [Fact]
    public async Task Handle_AWrittenOffLoanNotYetRecovered_MovesToo()
    {
        // The cut that separates this sweep from the edition one: a written-off loan still speaks
        // its borrower the day its copy resurfaces, so a late recovery must speak against the
        // account that now answers for the person.
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var writtenOff = ALoanOf(absorbed);
        writtenOff.DeclareLost(Today.AddDays(45));
        writtenOff.ClearDomainEvents();

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        writtenOff.BorrowerId.ShouldBe(surviving);
    }

    [Fact]
    public async Task Handle_LeavesTheLoansOfEveryOtherBorrowerAlone()
    {
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var another = BorrowerId.Generate();
        var bystander = ALoanOf(another);
        ALoanOf(absorbed);

        await Handle(absorbed, surviving);

        bystander.BorrowerId.ShouldBe(another);
        bystander.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("returned")]
    [InlineData("recovered")]
    public async Task Handle_LeavesALoanAnsweredForSayingWhoBorrowed(string ending)
    {
        // The sweep never loads these, which is why the command still succeeds: a returned loan
        // and a recovered one are the record of settled business, whoever's file it was settled
        // under.
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var answeredFor = ALoanOf(absorbed);

        if (ending == "returned")
        {
            answeredFor.Return(Today, CirculationPolicy.Current);
        }
        else
        {
            answeredFor.DeclareLost(Today.AddDays(45));
            answeredFor.RecordRecovery(Today.AddDays(90), CirculationPolicy.Current);
        }

        answeredFor.ClearDomainEvents();

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        answeredFor.BorrowerId.ShouldBe(absorbed);
        answeredFor.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ARecordWithNothingUnsettled_Succeeds()
    {
        // The ordinary case, and the one a refusal would turn into a message the announcing drain
        // replays forever.
        var outcome = await Handle(BorrowerId.Generate(), BorrowerId.Generate());

        outcome.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ARedeliveredAnnouncement_ChangesNothingASecondTime()
    {
        // Idempotent by the question it asks rather than by a mark it keeps: after the first pass
        // the absorbed identifier answers for no unsettled loan at all.
        var absorbed = BorrowerId.Generate();
        var surviving = BorrowerId.Generate();
        var loan = ALoanOf(absorbed);

        await Handle(absorbed, surviving);
        loan.ClearDomainEvents();

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        loan.BorrowerId.ShouldBe(surviving);
        loan.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_DemandsACommand()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await new RepointLoansOfMergedBorrowerCommandHandler(_loans).Handle(null!, Token));
    }
}
