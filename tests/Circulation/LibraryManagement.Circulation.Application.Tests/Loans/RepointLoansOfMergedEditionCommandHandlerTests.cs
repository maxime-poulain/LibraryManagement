using LibraryManagement.Circulation.Application.Loans.RepointLoansOfMergedEdition;
using LibraryManagement.Circulation.Application.Tests.TestDoubles;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Shared.Domain.Results;

namespace LibraryManagement.Circulation.Application.Tests.Loans;

/// <summary>
/// What a merge in Catalog costs this context's loans: the live ones answer to the survivor, and
/// the ended ones keep saying what was borrowed.
/// </summary>
public sealed class RepointLoansOfMergedEditionCommandHandlerTests
{
    private static readonly DateOnly Today = new(2026, 3, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly InMemoryLoanRepository _loans = new();

    private ValueTask<Result> Handle(EditionId absorbed, EditionId surviving)
        => new RepointLoansOfMergedEditionCommandHandler(_loans)
            .Handle(
                new RepointLoansOfMergedEditionCommand(absorbed.Value, surviving.Value),
                Token);

    private Loan ALoanOf(EditionId editionId)
    {
        var loan = Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            editionId,
            BorrowerId.Generate(),
            Today,
            CirculationPolicy.Current);

        _loans.With(loan);
        loan.ClearDomainEvents();

        return loan;
    }

    [Fact]
    public async Task Handle_MovesEveryLoanStillOutOnTheAbsorbedRecord()
    {
        var absorbed = EditionId.Generate();
        var surviving = EditionId.Generate();
        var outOnLoan = new[] { ALoanOf(absorbed), ALoanOf(absorbed), ALoanOf(absorbed) };

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        outOnLoan.ShouldAllBe(loan => loan.EditionId == surviving);
        outOnLoan.ShouldAllBe(loan => loan.DomainEvents.OfType<LoanRepointed>().Count() == 1);
    }

    [Fact]
    public async Task Handle_LeavesTheLoansOfEveryOtherRecordAlone()
    {
        var absorbed = EditionId.Generate();
        var surviving = EditionId.Generate();
        var another = EditionId.Generate();
        var bystander = ALoanOf(another);
        ALoanOf(absorbed);

        await Handle(absorbed, surviving);

        bystander.EditionId.ShouldBe(another);
        bystander.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("returned")]
    [InlineData("lost")]
    public async Task Handle_LeavesAnEndedLoanSayingWhatWasBorrowed(string ending)
    {
        // The rule the whole scope turns on: a merge moves live state and does not rewrite the
        // past. The sweep never loads these, which is why the command still succeeds.
        var absorbed = EditionId.Generate();
        var surviving = EditionId.Generate();
        var ended = ALoanOf(absorbed);

        if (ending == "returned")
        {
            ended.Return(Today, CirculationPolicy.Current);
        }
        else
        {
            ended.DeclareLost(Today);
        }

        ended.ClearDomainEvents();

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        ended.EditionId.ShouldBe(absorbed);
        ended.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ARecordWithNothingOut_Succeeds()
    {
        // The ordinary case — a cataloger merges records, not shelves — and the one a refusal would
        // turn into a message the announcing drain replays forever.
        var outcome = await Handle(EditionId.Generate(), EditionId.Generate());

        outcome.HasErrors().ShouldBeFalse();
    }

    [Fact]
    public async Task Handle_ARedeliveredAnnouncement_ChangesNothingASecondTime()
    {
        // Idempotent by the question it asks rather than by a mark it keeps: after the first pass
        // the absorbed identifier is on no live loan at all.
        var absorbed = EditionId.Generate();
        var surviving = EditionId.Generate();
        var loan = ALoanOf(absorbed);

        await Handle(absorbed, surviving);
        loan.ClearDomainEvents();

        var outcome = await Handle(absorbed, surviving);

        outcome.HasErrors().ShouldBeFalse();
        loan.EditionId.ShouldBe(surviving);
        loan.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_DemandsACommand()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await new RepointLoansOfMergedEditionCommandHandler(_loans).Handle(null!, Token));
    }
}
