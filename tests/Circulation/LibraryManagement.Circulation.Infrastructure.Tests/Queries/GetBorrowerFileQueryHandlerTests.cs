using LibraryManagement.Circulation.Application.Borrowers.GetBorrowerFile;
using LibraryManagement.Circulation.Domain.Holds;
using LibraryManagement.Circulation.Domain.Loans;
using LibraryManagement.Circulation.Infrastructure.Queries;
using LibraryManagement.Circulation.PublishedLanguage;

namespace LibraryManagement.Circulation.Infrastructure.Tests.Queries;

/// <summary>
/// The circulation half of the desk's member file, asked of the real store.
/// </summary>
/// <remarks>
/// Two things are under test that only a database can answer: that the holds read joins the queue
/// to its holds rather than walking the aggregates, and that a returned loan stays out of a file
/// that is meant to be the present tense.
/// </remarks>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Integration")]
public sealed class GetBorrowerFileQueryHandlerTests(SqlServerFixture sqlServer)
{
    private static readonly DateOnly Today = new(2026, 3, 14);
    private static readonly DateTimeOffset ThisMorning = new(2026, 3, 14, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Charges' answer, stood in for. What it forbids is judged here, never there.</summary>
    private sealed class Owing(decimal amount) : IMemberBalance
    {
        public ValueTask<decimal> OwedByAsync(Guid memberId, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(amount);
    }

    private static Loan ALoan(BorrowerId borrower, DateOnly? checkedOutOn = null)
        => Loan.CheckOut(
            LoanId.Generate(),
            CopyId.Generate(),
            EditionId.Generate(),
            borrower,
            checkedOutOn ?? Today,
            CirculationPolicy.Current);

    private async Task StoreAsync(params object[] aggregates)
    {
        await using var writing = sqlServer.NewContext();
        writing.AddRange(aggregates);
        await writing.SaveChangesAsync(Token);
    }

    private async Task<BorrowerFileDto> ReadAsync(BorrowerId borrower, decimal owed = 0m)
    {
        await using var reading = sqlServer.NewContext();

        var result = await new GetBorrowerFileQueryHandler(
                reading,
                new Owing(owed),
                CirculationPolicy.Current with { BlockingDebt = 10m })
            .Handle(new GetBorrowerFileQuery(borrower.Value), Token);

        return result.Match(
            file => file,
            errors => throw new InvalidOperationException(errors[0].ErrorMessage));
    }

    [Fact]
    public async Task ABorrowerWithNothingOnFile_IsAnEmptyFileAndNotARefusal()
    {
        // This context keeps no register of people, so there is no such thing as an unknown
        // borrower to refuse. Whether they are enrolled at all is Members' question.
        var file = await ReadAsync(BorrowerId.Generate());

        file.Loans.ShouldBeEmpty();
        file.Holds.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheFile_CarriesWhatIsOut_AndLeavesOtherBorrowersAlone()
    {
        var borrower = BorrowerId.Generate();
        var loan = ALoan(borrower);
        await StoreAsync(loan, ALoan(BorrowerId.Generate()));

        var file = await ReadAsync(borrower);

        var only = file.Loans.ShouldHaveSingleItem();
        only.LoanId.ShouldBe(loan.Id.Value);
        only.CopyId.ShouldBe(loan.CopyId.Value);
        only.EditionId.ShouldBe(loan.EditionId.Value);
        only.DueDate.ShouldBe(loan.DueDate);
        only.Status.ShouldBe(nameof(LoanStatus.Active));
    }

    [Fact]
    public async Task AReturnedLoan_IsHistoryAndStaysOutOfTheFile()
    {
        var borrower = BorrowerId.Generate();
        var returned = ALoan(borrower);
        returned.Return(Today, CirculationPolicy.Current).HasErrors().ShouldBeFalse();

        var lost = ALoan(borrower);
        lost.DeclareLost(Today).HasErrors().ShouldBeFalse();

        await StoreAsync(returned, lost);

        // The lost one stays: the copy has not come back, so the borrower still has to answer
        // for it. Returned is the only status that ends the conversation.
        var only = (await ReadAsync(borrower)).Loans.ShouldHaveSingleItem();

        only.LoanId.ShouldBe(lost.Id.Value);
        only.Status.ShouldBe(nameof(LoanStatus.DeclaredLost));
        only.DeclaredLostOn.ShouldBe(Today);
    }

    [Fact]
    public async Task HoldsAcrossQueues_ComeBackTogether_OldestClaimFirst()
    {
        // The read the borrower index was built for. A borrower's claims live in as many queues
        // as there are editions, and no aggregate spans them.
        var borrower = BorrowerId.Generate();

        var later = HoldQueue.For(EditionId.Generate());
        later.PlaceHold(HoldId.Generate(), borrower, ThisMorning.AddHours(2)).HasErrors().ShouldBeFalse();

        var earlier = HoldQueue.For(EditionId.Generate());
        earlier.PlaceHold(HoldId.Generate(), borrower, ThisMorning).HasErrors().ShouldBeFalse();
        earlier.PlaceHold(HoldId.Generate(), BorrowerId.Generate(), ThisMorning).HasErrors().ShouldBeFalse();

        await StoreAsync(later, earlier);

        var holds = (await ReadAsync(borrower)).Holds;

        holds.Count.ShouldBe(2);
        holds[0].EditionId.ShouldBe(earlier.Id.Value);
        holds[1].EditionId.ShouldBe(later.Id.Value);
        holds[0].Status.ShouldBe(nameof(HoldStatus.Queued));
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(10, false)]
    [InlineData(11, true)]
    public async Task TheFile_CarriesTheVerdictAndNeverTheAmount(decimal owed, bool blocked)
    {
        // Standing, judged here against this module's own threshold. The euros are on the page
        // too, and it is Charges that puts them there.
        var file = await ReadAsync(BorrowerId.Generate(), owed);

        file.DebtBlocksBorrowing.ShouldBe(blocked);
    }
}
